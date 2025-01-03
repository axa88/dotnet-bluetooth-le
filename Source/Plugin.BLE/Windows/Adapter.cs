using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Contracts.Pairing;
using Plugin.BLE.Extensions;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;

using Plugin.BLE.Abstractions.EventArgs;


namespace Plugin.BLE.Windows;

public class Adapter(BluetoothAdapter adapter) : AdapterBase, IBondReportable, IBondable, IPairProcess
{
	private BluetoothLEAdvertisementWatcher _bleWatcher;

	/// <summary>
	/// Registry used to store device instances for pending disconnect operations
	/// Helps to detect connection lost events.
	/// </summary>
	private readonly IDictionary<string, IDevice> _disconnectingRegistry = new ConcurrentDictionary<string, IDevice>();

	protected override Task StartScanningForDevicesNativeAsync(ScanFilterOptions scanFilterOptions, bool allowDuplicatesKey, CancellationToken scanCancellationToken)
	{
		var serviceUuids = scanFilterOptions?.ServiceUuids;
		var hasFilter = serviceUuids?.Any() ?? false;

		#pragma warning disable CA1416
		_bleWatcher = new() { ScanningMode = ScanMode.ToNative(), AllowExtendedAdvertisements = true };
		#pragma warning restore CA1416

		Trace.Message("Starting a scan for devices.");
		if (hasFilter)
		{
			//adds filter to native scanner if serviceUuids are specified
			foreach (var uuid in serviceUuids)
				_bleWatcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(uuid);

			Trace.Message($"ScanFilters: {string.Join(", ", serviceUuids)}");
		}

		_bleWatcher.Received += AdvertisementReceived;
		_bleWatcher.Start();
		return Task.FromResult(true);
	}

	protected override void StopScanNative()
	{
		if (_bleWatcher != null)
		{
			Trace.Message("Stopping the scan for devices");
			_bleWatcher.Stop();
			_bleWatcher.Received -= AdvertisementReceived;
			_bleWatcher = null;
		}
	}

	protected override async Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters, CancellationToken cancellationToken)
	{
		var dev = (Device)device;
		if (dev.NativeDevice == null)
			await dev.RecreateNativeDevice();

		var nativeDevice = (BluetoothLEDevice)device.NativeDevice;
		Trace.Message($"ConnectToDeviceNativeAsync {device.Id.ToHexBleAddress()} Named: {device.Name} Connected: {nativeDevice.ConnectionStatus}");

		var success = await dev.ConnectInternal(connectParameters, cancellationToken);
		if (success)
		{
			if (!ConnectedDeviceRegistry.ContainsKey(device.Id.ToString()))
			{
				ConnectedDeviceRegistry[device.Id.ToString()] = device;
				nativeDevice.ConnectionStatusChanged += ConnectionStatusChanged;
				if (nativeDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
					ConnectionStatusChanged(nativeDevice, null);
			}
		}
		else
		{
			// use DisconnectDeviceNative to clean up resources otherwise windows won't disconnect the device after a subsequent successful connection (#528, #536, #423)
			DisconnectDeviceNative(device);

			// trigger connection failed event
			HandleConnectionFail(device, "Failed connecting to device.");

			// this is normally done in ConnectionStatusChanged but since nothing actually connected or disconnect, ConnectionStatusChanged will not trigger.
			ConnectedDeviceRegistry.TryRemove(device.Id.ToString(), out _);
		}
	}

	private void ConnectionStatusChanged(BluetoothLEDevice nativeDevice, object args)
	{
		Trace.Message($"{nameof(ConnectionStatusChanged)} {nativeDevice.BluetoothAddress.ToHexBleAddress()} {nativeDevice.Name} {nativeDevice.ConnectionStatus}");
		var id = nativeDevice.BluetoothAddress.ParseDeviceId().ToString();

		if (nativeDevice.ConnectionStatus == BluetoothConnectionStatus.Connected && ConnectedDeviceRegistry.TryGetValue(id, out var connectedDevice))
		{
			#if WINDOWS10_0_22000_0_OR_GREATER
			if (Environment.OSVersion.Version.Build >= 22000)
			{
				var connectionParameters = nativeDevice.GetConnectionParameters();
				Trace.Message($"Connected with Latency = {connectionParameters.ConnectionLatency}, Interval = {connectionParameters.ConnectionInterval}, Timeout = {connectionParameters.LinkTimeout}");
			}
			#endif
			HandleConnectedDevice(connectedDevice);
			return;
		}

		if (nativeDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected && ConnectedDeviceRegistry.TryRemove(id, out var disconnectedDevice))
		{
			var disconnectRequested = _disconnectingRegistry.Remove(id);
			if (!disconnectRequested)
				((Device)disconnectedDevice).DisconnectInternal(); // call to clean up on unsolicited disconnection else windows will not disconnect on a subsequent connect/disconnection

			ConnectedDeviceRegistry.Remove(id, out _);
			nativeDevice.ConnectionStatusChanged -= ConnectionStatusChanged;
			// fire the correct event (DeviceDisconnected or DeviceConnectionLost)
			HandleDisconnectedDevice(disconnectRequested, disconnectedDevice);
		}
	}

	protected override void DisconnectDeviceNative(IDevice device)
	{
		// Windows doesn't support disconnecting, so currently just dispose of the device
		Trace.Message($"{nameof(DisconnectDeviceNative)} from device ID: {device.Id.ToHexBleAddress()}");
		_disconnectingRegistry[device.Id.ToString()] = device;
		((Device)device).DisconnectInternal();
	}

	// ReSharper disable OptionalParameterHierarchyMismatch
	protected override async Task<IDevice> ConnectToKnownDeviceNativeAsync(Guid deviceGuid, ConnectParameters connectParameters, CancellationToken cancellationToken) // ReSharper restore OptionalParameterHierarchyMismatch
	{
		var nativeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(deviceGuid.ToBleAddress()) ?? throw new Abstractions.Exceptions.DeviceConnectionException(deviceGuid, "", $"[Adapter] Device {deviceGuid} not found.");
		var knownDevice = new Device(this, nativeDevice, sbyte.MaxValue, deviceGuid);
		await ConnectToDeviceAsync(knownDevice, connectParameters, cancellationToken: cancellationToken);
		return knownDevice;
	}

	/// <summary>
	/// Parses a given advertisement for various stored properties
	/// Currently only parses the manufacturer specific data
	/// </summary>
	/// <param name="ad">The advertisement to parse</param>
	/// <returns>List of generic advertisement records</returns>
	public static List<AdvertisementRecord> ParseAdvertisementData(BluetoothLEAdvertisement ad)
		=> ad.DataSections.Select(data => new AdvertisementRecord((AdvertisementRecordType)data.DataType, data.Data?.ToArray())).ToList();

	/// <summary>
	/// Handler for devices found when duplicates are not allowed
	/// </summary>
	/// <param name="watcher">The bluetooth advertisement watcher currently being used</param>
	/// <param name="ad">The advertisement received by the watcher</param>
	private void AdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs ad)
	{
		var deviceId = ad.BluetoothAddress.ParseDeviceId();

		if (DiscoveredDevicesRegistry.TryGetValue(deviceId, out var device))
		{
			// This deviceId has been discovered
			Trace.Message($"{nameof(AdvertisementReceived)} - Old: {0}", ad.ToDetailedString(device.Name));
			(device as Device)?.Update(ad.RawSignalStrengthInDBm, ParseAdvertisementData(ad.Advertisement));
			HandleDiscoveredDevice(device);
		}
		else
		{
			var bluetoothLeDevice = BluetoothLEDevice.FromBluetoothAddressAsync(ad.BluetoothAddress).AsTask().Result;
			if (bluetoothLeDevice != null) //make sure advertisement bluetooth address actually returns a device
			{
				#pragma warning disable CA1416
				device = new Device(this, bluetoothLeDevice, ad.RawSignalStrengthInDBm, deviceId, ParseAdvertisementData(ad.Advertisement), ad.IsConnectable);
				#pragma warning restore CA1416
				Trace.Message("AdvReceived - New: {0}", ad.ToDetailedString(device.Name));
				HandleDiscoveredDevice(device);
			}
		}
	}

	public override IReadOnlyList<IDevice> GetConnectedOrBondedDevices(Guid[] services = null)
	{
		var pairedSelector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
		DeviceInformationCollection pairedDevices = DeviceInformation.FindAllAsync(pairedSelector).GetAwaiter().GetResult();
		List<IDevice> devices = ConnectedDevices.ToList();
		List<Guid> ids = ConnectedDevices.Select(d => d.Id).ToList();
		foreach (var dev in pairedDevices)
		{
			var id = dev.Id.ToBleDeviceGuidFromId();
			var bleAddress = id.ToBleAddress();
			if (!ids.Contains(id))
			{
				var bluetoothLeDevice = BluetoothLEDevice.FromBluetoothAddressAsync(bleAddress).AsTask().Result;
				if (bluetoothLeDevice != null)
				{
					var device = new Device(this, bluetoothLeDevice, sbyte.MaxValue, id);
					devices.Add(device);
					ids.Add(id);
					Trace.Message($"{nameof(GetConnectedOrBondedDevices)}: {dev.Id}: {dev.Name}");
				}
				else
					Trace.Message($"{nameof(GetConnectedOrBondedDevices)}: {dev.Id}: {dev.Name}, BluetoothLEDevice == null");

			}
		}
		return devices;
	}

	public override IReadOnlyList<IDevice> GetConnectedOrBondedDevicesByIds(Guid[] ids) => []; // TODO: implement this

	#pragma warning disable CA1416
	public override bool SupportsExtendedAdvertising() => adapter.IsExtendedAdvertisingSupported;
	#pragma warning restore CA1416

	#region Implementation of IBondReportable

	public event EventHandler<DeviceBondStateChangedEventArgs> DeviceBondStateChanged;

	public IReadOnlyList<IDevice> BondedDevices => GetBondedDevices();

	private List<IDevice> GetBondedDevices()
	{
		var pairedSelector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
		var pairedDevices = DeviceInformation.FindAllAsync(pairedSelector).GetAwaiter().GetResult();
		List<IDevice> devices = [];
		foreach (var deviceInfo in pairedDevices)
		{
			var id = deviceInfo.Id.ToBleDeviceGuidFromId();
			var bluetoothLeDevice = BluetoothLEDevice.FromBluetoothAddressAsync(id.ToBleAddress()).AsTask().Result;
			if (bluetoothLeDevice != null)
			{
				devices.Add(new Device(this, bluetoothLeDevice, sbyte.MaxValue, id));
				Trace.Message($"GetBondedDevices: {deviceInfo.Id}: {deviceInfo.Name}");
			}
			else
				Trace.Message($"GetBondedDevices: {deviceInfo.Id}: {deviceInfo.Name}, BluetoothLEDevice == null");
		}
		return devices;
	}

	#endregion

	#region Implementation of IBondable

	public async Task<BondResult> BondAsync(IDevice device, BondingOptions options, CancellationToken cancellationToken)
	{
		// ToDo: should these return a failed result instead?
		if (device == null)
			throw new ArgumentNullException(nameof(device), "Invalid Device");

		if (device.NativeDevice is not BluetoothLEDevice bluetoothLeDevice)
			throw new ArgumentException($"Invalid argument property {nameof(device.NativeDevice)}", nameof(device));

		DeviceInformation deviceInformation = null;
		try
		{
			deviceInformation = await DeviceInformation.CreateFromIdAsync(bluetoothLeDevice.DeviceId).AsTask(cancellationToken);

			if (deviceInformation.Pairing.IsPaired)
			{
				const DevicePairingResultStatus status = DevicePairingResultStatus.AlreadyPaired;
				return new(status.XPlatformPairStatus(), $"{status}");
			}

			if (!deviceInformation.Pairing.CanPair)
			{
				const DevicePairingResultStatus status = DevicePairingResultStatus.NotReadyToPair;
				return new(status.XPlatformPairStatus(), $"{status}");
			}

			cancellationToken.ThrowIfCancellationRequested(); // check for cancel after allowing it to use the awaited to exit gracefully, but before subsequent awaited code

			deviceInformation.Pairing.Custom.PairingRequested += OnPairingRequested;

			DevicePairingResult result;
			if (options == null)
				result = await deviceInformation.Pairing.PairAsync().AsTask(cancellationToken); // support legacy versions of this API (just works?)
			else
			{
				var requestedModes = (DevicePairingKinds)options.RequestedModes;
				var requestedProtection = (DevicePairingProtectionLevel)options.MinimumRequestedProtection;
				result = await deviceInformation.Pairing.Custom.PairAsync(requestedModes, requestedProtection).AsTask(cancellationToken);
			}

			Trace.Message($"Pairing {nameof(result)}: {result.Status}");
			return new(result.Status.XPlatformPairStatus(), $"{result.Status}", (ProtectionLevel)result.ProtectionLevelUsed);
		}
		catch (Exception exception)
		{
			Trace.Message(exception.Message);
			throw;
		}
		finally
		{
			if (deviceInformation != null)
				deviceInformation.Pairing.Custom.PairingRequested -= OnPairingRequested;
		}
	}

	private void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
	{
		// ToDo: should this throw instead?
		if (args.PairingKind == DevicePairingKinds.None || (args.PairingKind & (args.PairingKind - 1)) != 0)
		{
			Trace.Message($"Remote devices indicates it does not support pairing or has chosen more than one pairing mode?! the latter shouldn't be possible ");
			return;
		}

		using var deferral = args.GetDeferral(); // ToDo: remove deferral and see if it matters
		var negotiatedPairingMode = (PairModes)args.PairingKind;
		Trace.Message($"Negotiated Pairing Mode: {negotiatedPairingMode}");
		PairResponded?.Invoke(this, new(negotiatedPairingMode, OnPairingResponded, args.Pin));
		deferral.Complete();

		// no point to pass the cancellationToken as if the client has already responded it might as well complete.
		void OnPairingResponded(IPairProcess.IPairResponse response)
		{
			switch (negotiatedPairingMode)
			{
				case PairModes.None:
					break;
				case PairModes.Consent:
				case PairModes.DisplayPin:
				case PairModes.ConfirmPinMatch:
					args.Accept();
					break;
				case PairModes.ProvidePin when response is IPairProcess.PinPairResponse pinResponse:
					args.Accept(pinResponse.Pin);
					break;
				case PairModes.ProvidePasswordCredential when response is IPairProcess.CredentialsPairResponse credentialsResponse:
					if (!string.IsNullOrWhiteSpace(credentialsResponse.UserName) && !string.IsNullOrWhiteSpace(credentialsResponse.Password))
					#pragma warning disable CA1416
						args.AcceptWithPasswordCredential(new(credentialsResponse.Resource, credentialsResponse.UserName, credentialsResponse.Password));
					#pragma warning restore CA1416
					else
						Trace.Message($"Accepting the {negotiatedPairingMode} mode requires a User Name:<{credentialsResponse.UserName}> and Password: <{credentialsResponse.Password}>");
					break;
				default:
					Trace.Message($"New pairing mode not supported: {negotiatedPairingMode}");
					break;
			}
		}
	}

	#endregion

	#region Implementation of IPairProcess

	public event EventHandler<IPairProcess.PairRespondedEventArgs> PairResponded;

	#endregion
}
