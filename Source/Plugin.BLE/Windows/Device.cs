using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Extensions;
using Plugin.BLE.Shared.Contracts.Pairing;
using Plugin.BLE.Shared.Contracts.Rssi;


namespace Plugin.BLE.Windows;

public class Device : DeviceBase<BluetoothLEDevice>, IBondState
{
	private GattSession _gattSession;
	private bool _isDisposed;

	public Device(IAdapter adapter, Guid id, string name, bool isConnectable) : base(adapter, isConnectable)
	{
		Id = id;
		Rssi = new RssiBase();
		Name = !string.IsNullOrWhiteSpace(name) ? name : Name;
		NativeDevice = BluetoothLEDevice.FromBluetoothAddressAsync(id.ToBleAddress()).GetAwaiter().GetResult(); // ToDO deal with this
		if (NativeDevice != null)
		{
			DeviceId = NativeDevice.DeviceId;
			Name = string.IsNullOrWhiteSpace(NativeDevice.Name) ? Name : NativeDevice.Name;
			CanPair = NativeDevice.DeviceInformation.Pairing.CanPair;

			NativeDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

			Trace.Message($"{nameof(NativeDevice.BluetoothDeviceId.Id)}: {NativeDevice.BluetoothDeviceId.Id}");
			Trace.Message($"{nameof(NativeDevice.DeviceId)}: {NativeDevice.DeviceId}");
		}

		Trace.Message($"Constructed: native : {(NativeDevice == null ? "nul" : "object")}");
	}

	~Device() => DisposeGattSession();

	protected BluetoothLEDevice BluetoothLeDevice { get; private set; } // ToDo use this to replace NativeDevice as platform specific objects shouldn't be in the contract or even in the base, but if it is it should be overridable with maximum protection
	public bool? CanPair { get; protected internal set; }
	public bool IsBonded { get; protected internal set; }
	public bool IsConnected { get; protected internal set; }
	protected internal string DeviceId { get; set; }

	public sealed override string Name { get; protected internal set; } = "";

	public override Task<IRssi> GetRssi(CancellationToken cancellationToken = default) => Task.FromResult(Rssi);

	public override bool UpdateConnectionParameters(ConnectParameters connectParameters = default) => RequestPreferredConnectionParameters(NativeDevice, connectParameters);

	public DeviceBondState BondState => !IsConnectable ? DeviceBondState.NotSupported : IsBonded ? DeviceBondState.Bonded : DeviceBondState.NotBonded;

	protected internal async Task<bool> ConnectInternal(ConnectParameters connectParameters, CancellationToken cancellationToken)
	{
		if (SupportsIsConnectable && !IsConnectable)
			return await Task.FromResult(false);

		try
		{
			_gattSession = await GattSession.FromDeviceIdAsync(BluetoothDeviceId.FromId(DeviceId)).AsTask(cancellationToken);
			if (_gattSession is not null)
			{
				_gattSession.MaintainConnection = true;
				_gattSession.SessionStatusChanged += OnGattSessionStatusChanged;
				_gattSession.MaxPduSizeChanged += OnGattSessionMaxPduSizeChanged;

				RequestPreferredConnectionParameters(NativeDevice, connectParameters);
			}
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
			DisposeGattSession();
			throw;
		}

		return await Task.FromResult(_gattSession != null);
	}

	protected internal void DisconnectInternal()
	{
		DisposeGattSession();
		ClearServices();
		DisposeNativeDevice();
	}

	protected override async Task<IReadOnlyList<IService>> GetServicesNativeAsync(CancellationToken cancellationToken)
	{
		if (NativeDevice == null)
			return new List<IService>();

		var result = await NativeDevice.GetGattServicesAsync(BleImplementation.CacheModeGetServices);
		result?.ThrowIfError();
		return result?.Services?.Select(nativeService => new Service(nativeService, this)).Cast<IService>().ToList() ?? [];
	}

	protected override async Task<IService> GetServiceNativeAsync(Guid id, CancellationToken cancellationToken)
	{
		var result = await NativeDevice.GetGattServicesForUuidAsync(id, BleImplementation.CacheModeGetServices);
		result.ThrowIfError();

		var nativeService = result.Services?.FirstOrDefault();
		return nativeService != null ? new Service(nativeService, this) : null;
	}

	public override DeviceState State => IsConnected ? DeviceState.Connected : DeviceState.Disconnected;

	//protected override DeviceState GetState() => NativeDevice?.ConnectionStatus == BluetoothConnectionStatus.Connected ? DeviceState.Connected : DeviceState.Disconnected;

	protected override Task<int> RequestMtuNativeAsync(int requestValue, CancellationToken cancellationToken)
	{
		// Ref https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattsession.maxpdusize
		// There are no means in windows to request a change, but we can read the current value
		if (_gattSession is null)
		{
			Trace.Message("WARNING RequestMtuNativeAsync failed since gattSession is null");
			return Task.FromResult(-1);
		}
		return Task.FromResult<int>(_gattSession.MaxPduSize);
	}

	protected override bool UpdateConnectionIntervalNative(ConnectionInterval interval)
	{
		Trace.Message("Update Connection Interval not supported in Windows");
		return false;
	}

	protected internal async Task<bool> VerifyUnderlyingDevice([CallerMemberName] string caller = null)
	{
		if (NativeDevice == null)
		{
			NativeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(Id.ToBleAddress());
			//NativeDevice = await BluetoothLEDevice.FromIdAsync(DeviceId);

			if (IsConnectable && NativeDevice == null)
				Trace.Message($"********** Holy shit a connectable device cant get underlying ***********");

			if (NativeDevice != null) // else must be a non-connectable beacon type
			{
				DeviceId = NativeDevice.DeviceId;
				Name = string.IsNullOrWhiteSpace(Name) ? Name : NativeDevice.Name;
				CanPair = NativeDevice.DeviceInformation.Pairing.CanPair;
			}
		}
		return NativeDevice != null;
	}

	private static bool RequestPreferredConnectionParameters(BluetoothLEDevice device, ConnectParameters connectParameters)
	{
		if (device?.ConnectionStatus != BluetoothConnectionStatus.Connected)
			return false;

		#if WINDOWS10_0_22000_0_OR_GREATER
		var parameters = connectParameters.ConnectionParameterSet switch
		{
			ConnectionParameterSet.Balanced => BluetoothLEPreferredConnectionParameters.Balanced,
			ConnectionParameterSet.PowerOptimized => BluetoothLEPreferredConnectionParameters.PowerOptimized,
			ConnectionParameterSet.ThroughputOptimized => BluetoothLEPreferredConnectionParameters.ThroughputOptimized,
			_ => null
		};

		if (parameters == null)
			return false;

		return device.RequestPreferredConnectionParameters(parameters).Status == BluetoothLEPreferredConnectionParametersRequestStatus.Success;
		#else
		return false;
		#endif
	}

	private static void OnConnectionStatusChanged(BluetoothLEDevice nativeDevice, object args) { Trace.Message($"{nameof(nativeDevice.ConnectionStatus)}: {nativeDevice.ConnectionStatus}"); }

	private void OnGattSessionStatusChanged(GattSession session, GattSessionStatusChangedEventArgs args)
	{
		Trace.Message($"{nameof(OnGattSessionStatusChanged)} => {nameof(args.Status)}: {args.Status}, {nameof(args.Error)}: {args.Error}");
		Trace.Message($"{nameof(DeviceState)}: {State}");

		switch (args.Status)
		{
			case GattSessionStatus.Closed:
			case GattSessionStatus.Active:
				Trace.Message($"{nameof(GattSessionStatus)} {args.Status}");
				break;
		}
	}

	private static void OnGattSessionMaxPduSizeChanged(GattSession sender, object args)
	{
		Trace.Message("GattSession_MaxPduSizeChanged: {0}", sender.MaxPduSize);
	}

	private void DisposeNativeDevice()
	{
		if (NativeDevice != null)
		{
			NativeDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
			NativeDevice?.Dispose();
			NativeDevice = null;
		}
	}

	private void DisposeGattSession()
	{
		if (_gattSession != null)
		{
			_gattSession.MaintainConnection = false;
			_gattSession.MaxPduSizeChanged -= OnGattSessionMaxPduSizeChanged;
			_gattSession.SessionStatusChanged -= OnGattSessionStatusChanged;
			_gattSession.Dispose();
			_gattSession = null;
		}
	}

	public override void Dispose()
	{
		if (_isDisposed)
			return;

		_isDisposed = true;

		try
		{
			DisposeGattSession();
			ClearServices();
			DisposeNativeDevice();
		}
		catch
		{
			// ignored
		}
	}
}