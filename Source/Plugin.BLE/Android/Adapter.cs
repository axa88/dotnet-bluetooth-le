using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.OS;

using Java.Util;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Android.Extensions;
using Plugin.BLE.BroadcastReceivers;
using Plugin.BLE.Extensions;
using Plugin.BLE.Shared.Contracts.Pairing;
using Plugin.BLE.Shared.Contracts.Pairing.Adapter;
using Plugin.BLE.Shared.Contracts.RequestResults;

using BondState = Android.Bluetooth.Bond;
using Object = Java.Lang.Object;
using Trace = Plugin.BLE.Abstractions.Trace;


namespace Plugin.BLE.Android;

public class Adapter : AdapterBase, IBondReport, IBondRequest
{
	private readonly BluetoothManager _bluetoothManager;
	private readonly BluetoothAdapter _bluetoothAdapter;
	private readonly Api18BleScanCallback _api18ScanCallback;
	private readonly Api21BleScanCallback _api21ScanCallback;

	private readonly Dictionary<string, TaskCompletionSource<IResult>> _bondingTaskSources = [];

	public Adapter(BluetoothManager bluetoothManager)
	{
		_bluetoothManager = bluetoothManager;
		_bluetoothAdapter = bluetoothManager.Adapter;

		// Issue: #822, #883 Sync ConnectedDevices and Device.State with Bluetooth.State, for when the radio is disabled, all devices disconnect.
		var bluetoothStateChanged = new BluetoothStatusBroadcastReceiver(state =>
		{
			if (state is BluetoothState.Off)
			{
				foreach (var connectedDevice in ConnectedDeviceRegistry)
					HandleDisconnectedDevice(false, connectedDevice.Value, "Device Disconnects when Bluetooth is Disabled");

				ConnectedDeviceRegistry.Clear();
			}
		});
		_ = Application.Context.RegisterReceiver(bluetoothStateChanged, new (BluetoothAdapter.ActionStateChanged));

		var bondStatusBroadcastReceiver = new BondStatusBroadcastReceiver(this);
		_ = Application.Context.RegisterReceiver(bondStatusBroadcastReceiver, new(BluetoothDevice.ActionBondStateChanged));

		bondStatusBroadcastReceiver.BondStateChanged += (_, args) =>
		{
			DeviceBondStateChanged?.Invoke(this, args);

			var address = args.Address;
			if (_bondingTaskSources.TryGetValue(address, out var tcsResult))
			{
				if (args.State != DeviceBondState.Bonding)
				{
					_bondingTaskSources.Remove(address);
					var bondState = ((BluetoothDevice)args.Device.NativeDevice).BondState;
					tcsResult.TrySetResult(new BondResult(bondState.XPlatformBondStatus(), $"{bondState.XPlatformBondState()}"));
				}
			}
		};

		if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
			_api21ScanCallback = new(this);
		else
			_api18ScanCallback = new(this);
	}

	protected override Task StartScanningForDevicesNativeAsync(ScanFilterOptions scanFilterOptions, bool allowDuplicatesKey, CancellationToken scanCancellationToken)
	{
		if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
			StartScanningOld(scanFilterOptions?.ServiceUuids);
		else
			StartScanningNew(scanFilterOptions);

		return Task.FromResult(true);
	}

	private void StartScanningOld(Guid[] serviceUuids)
	{
		var hasFilter = serviceUuids?.Any() ?? false;
		UUID[] uuids = null;
		if (hasFilter)
		{
			uuids = serviceUuids.Select(u => UUID.FromString(u.ToString())).ToArray();
		}
		Trace.Message("Adapter < 21: Starting a scan for devices.");
		#pragma warning disable 618
		_bluetoothAdapter.StartLeScan(uuids, _api18ScanCallback);
		#pragma warning restore 618
	}

	private void StartScanningNew(ScanFilterOptions scanFilterOptions)
	{
		var hasFilter = scanFilterOptions?.HasFilter == true;
		List<ScanFilter> scanFilters = null;

		if (hasFilter)
		{
			scanFilters = [];
			if (scanFilterOptions.HasServiceIds)
			{
				foreach (var serviceUuid in scanFilterOptions.ServiceUuids)
				{
					var sfb = new ScanFilter.Builder();
					sfb.SetServiceUuid(ParcelUuid.FromString(serviceUuid.ToString()));
					scanFilters.Add(sfb.Build());
				}
			}
			if (scanFilterOptions.HasServiceData)
			{
				foreach (var serviceDataFilter in scanFilterOptions.ServiceDataFilters)
				{
					var sfb = new ScanFilter.Builder();
					if (serviceDataFilter.ServiceDataMask == null)
						sfb.SetServiceData(ParcelUuid.FromString(serviceDataFilter.ServiceDataUuid.ToString()), serviceDataFilter.ServiceData);
					else
						sfb.SetServiceData(ParcelUuid.FromString(serviceDataFilter.ServiceDataUuid.ToString()), serviceDataFilter.ServiceData, serviceDataFilter.ServiceDataMask);
					scanFilters.Add(sfb.Build());
				}
			}
			if (scanFilterOptions.HasManufacturerIds)
			{
				foreach (var manufacturerDataFilter in scanFilterOptions.ManufacturerDataFilters)
				{
					var sfb = new ScanFilter.Builder();
					if (manufacturerDataFilter.ManufacturerDataMask != null)
						sfb.SetManufacturerData(manufacturerDataFilter.ManufacturerId, manufacturerDataFilter.ManufacturerData);
					else
						sfb.SetManufacturerData(manufacturerDataFilter.ManufacturerId, manufacturerDataFilter.ManufacturerData, manufacturerDataFilter.ManufacturerDataMask);
					scanFilters.Add(sfb.Build());
				}
			}
			if (scanFilterOptions.HasDeviceAddresses)
			{
				foreach (var deviceAddress in scanFilterOptions.DeviceAddresses)
				{
					if (BluetoothAdapter.CheckBluetoothAddress(deviceAddress))
					{
						var sfb = new ScanFilter.Builder();
						sfb.SetDeviceAddress(deviceAddress);
						scanFilters.Add(sfb.Build());
					}
					else
					{
						Trace.Message($"Device address {deviceAddress} is invalid. The correct format is \"01:02:03:AB:CD:EF\"");
					}

				}
			}
			if (scanFilterOptions.HasDeviceNames)
			{
				foreach (var deviceName in scanFilterOptions.DeviceNames)
				{
					var sfb = new ScanFilter.Builder();
					sfb.SetDeviceName(deviceName);
					scanFilters.Add(sfb.Build());
				}
			}

		}

		var ssb = new ScanSettings.Builder();
		ssb.SetScanMode(ScanMode.ToNative());

		#if NET6_0_OR_GREATER
		if (OperatingSystem.IsAndroidVersionAtLeast(23))
		#else
		if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
		#endif
		{
			// set the match mode on Android 6 and above
			ssb.SetMatchMode(ScanMatchMode.ToNative());

			// If set to agressive, reduce the number of adverts needed before raising the DeviceFound callback
			if (ScanMatchMode.ToNative() == BluetoothScanMatchMode.Aggressive)
			{
				// Be more agressive when seeking adverts
				ssb.SetNumOfMatches((int)BluetoothScanMatchNumber.OneAdvertisement);
				Trace.Message("Using ScanMatchMode Aggressive");
			}
		}

		#if NET6_0_OR_GREATER
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		#else
		if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
		#endif
		{
			// enable Bluetooth 5 Advertisement Extensions on Android 8.0 and above
			ssb.SetLegacy(false);
		}
		//ssb.SetCallbackType(ScanCallbackType.AllMatches);

		if (_bluetoothAdapter.BluetoothLeScanner != null)
		{
			Trace.Message($"Adapter >=21: Starting a scan for devices. ScanMode: {ScanMode}");
			if (hasFilter)
			{
				if (scanFilterOptions.HasServiceIds)
					Trace.Message($"Service UUID Scan Filters: {string.Join(", ", scanFilterOptions.ServiceUuids)}");
				if (scanFilterOptions.HasServiceData)
					Trace.Message($"Service Data Scan Filters: {string.Join(", ", scanFilterOptions.ServiceDataFilters.ToString())}");
				if (scanFilterOptions.HasManufacturerIds)
					Trace.Message($"Manufacturer Id Scan Filters: {string.Join(", ", scanFilterOptions.ManufacturerDataFilters.ToString())}");
				if (scanFilterOptions.HasDeviceAddresses)
					Trace.Message($"Device Address Scan Filters: {string.Join(", ", scanFilterOptions.DeviceAddresses)}");
				if (scanFilterOptions.HasDeviceNames)
					Trace.Message($"Device Name Scan Filters: {string.Join(", ", scanFilterOptions.DeviceNames)}");
			}
			_bluetoothAdapter.BluetoothLeScanner.StartScan(scanFilters, ssb.Build(), _api21ScanCallback);
		}
		else
			Trace.Message("Adapter >= 21: Scan failed. Bluetooth is probably off");
	}

	protected override void StopScanNative()
	{
		if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
		{
			Trace.Message("Adapter < 21: Stopping the scan for devices.");
			#pragma warning disable 618
			_bluetoothAdapter.StopLeScan(_api18ScanCallback);
			#pragma warning restore 618
		}
		else
		{
			Trace.Message("Adapter >= 21: Stopping the scan for devices.");
			_bluetoothAdapter.BluetoothLeScanner?.StopScan(_api21ScanCallback);
		}
	}

	protected override Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters, CancellationToken cancellationToken)
	{
		((Device)device).Connect(connectParameters, cancellationToken);
		return Task.CompletedTask;
	}

	protected override void DisconnectDeviceNative(IDevice device) => ((Device)device).Disconnect(); //make sure everything is disconnected

	protected override async Task<IDevice> ConnectToKnownDeviceNativeAsync(Guid deviceGuid, ConnectParameters connectParameters, CancellationToken cancellationToken)
	{
		var macBytes = deviceGuid.ToByteArray().Skip(10).Take(6).ToArray();
		var nativeDevice = _bluetoothAdapter.GetRemoteDevice(macBytes);
		if (nativeDevice == null)
			throw new Abstractions.Exceptions.DeviceConnectionException(deviceGuid,"", $"[Adapter] Device {deviceGuid} not found.");
		if (!nativeDevice.SupportsBLE())
			throw new Abstractions.Exceptions.DeviceConnectionException(deviceGuid,"", $"[Adapter] Device {deviceGuid} does not support BLE.");
		var device = new Device(this, nativeDevice, null);

		await ConnectToDeviceAsync(device, connectParameters, cancellationToken);
		return device;
	}

	public override IReadOnlyList<IDevice> GetConnectedOrBondedDevices(Guid[] services = null)
	{
		if (services != null)
			Trace.Message("Caution: GetSystemConnectedDevices does not take into account the 'services' parameter on Android.");

		//add dualMode type also as they are BLE as well)
		var connectedDevices = (_bluetoothManager.GetConnectedDevices(ProfileType.Gatt) ?? new List<BluetoothDevice>()).Where(d => d.SupportsBLE());
		var bondedDevices = (_bluetoothAdapter.BondedDevices ?? new List<BluetoothDevice>()).Where(d => d.SupportsBLE());
		return connectedDevices.Union(bondedDevices, new DeviceComparer()).Select(d => new Device(this, d, null)).Cast<IDevice>().ToList();
	}

	public override IReadOnlyList<IDevice> GetConnectedOrBondedDevicesByIds(Guid[] ids) => GetConnectedOrBondedDevices().Where(item => ids.Contains(item.Id)).ToList();

	public override bool SupportsExtendedAdvertising()
	{
		#if NET6_0_OR_GREATER
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		#else
		if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
		#endif
			return _bluetoothAdapter.IsLeExtendedAdvertisingSupported;
		else
			return false;
	}

	public override bool SupportsCodedPhy()
	{
		#if NET6_0_OR_GREATER
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		#else
		if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
		#endif
			return _bluetoothAdapter.IsLeCodedPhySupported;
		else
			return false;
	}


	private class DeviceComparer : IEqualityComparer<BluetoothDevice>
	{
		public bool Equals(BluetoothDevice x, BluetoothDevice y) => x?.Address == y?.Address;

		public int GetHashCode(BluetoothDevice obj) => obj.GetHashCode();
	}

	#region Implementation of IBondReport

	public event EventHandler<DeviceBondStateChangedEventArgs> DeviceBondStateChanged;

	public IReadOnlyList<IDevice> BondedDevices => (_bluetoothAdapter.BondedDevices ?? new List<BluetoothDevice>()).Where(static d => d.SupportsBLE()).Select(d => new Device(this, d, null)).Cast<IDevice>().ToList();

	#endregion

	#region Implementation of IBondRequest

	public Task<IResult> Bond(IDevice device, BondingOptions bondingOptions = null, CancellationToken cancellationToken = default)
	{
		// ToDo: should these return a failed result instead?
		if (device == null)
			throw new ArgumentNullException(nameof(device));

		if (device.NativeDevice is not BluetoothDevice nativeDevice)
			throw new ArgumentException($"{device.NativeDevice} native device is invalid");

		if (string.IsNullOrWhiteSpace(nativeDevice.Address))
			throw new ArgumentException($"{device.NativeDevice} device with no address");

		var deviceAddress = nativeDevice.Address;
		var bondTaskSource = new TaskCompletionSource<IResult>();

		// prevent an additional attempt as here is the only place to know why it failed
		if (nativeDevice.BondState is BondState.Bonded or BondState.Bonding)
		{
			bondTaskSource.SetResult(new BondResult(nativeDevice.BondState.XPlatformBondStatus(), $"{nativeDevice.BondState.XPlatformBondState()}"));
			return bondTaskSource.Task;
		}

		// if a tsc cant be added return this request completed.
		// A pending bond request exists and should be allowed to finish, not randomly canceled by any and every subsequent call
		if (!_bondingTaskSources.TryAdd(deviceAddress, bondTaskSource))
		{
			bondTaskSource.TrySetResult(new BondResult(ResultStatus.SpecifiedFailure, "OperationAlreadyInProgress"));
			return bondTaskSource.Task;
		}

		if (!nativeDevice.CreateBond())
		{
			_bondingTaskSources.Remove(deviceAddress);
			bondTaskSource.TrySetResult(new BondResult(ResultStatus.UnspecifiedFailure));
		}

		return bondTaskSource.Task;
	}

	#endregion

	public class Api18BleScanCallback(Adapter adapter) : Object, BluetoothAdapter.ILeScanCallback
	{
		public void OnLeScan(BluetoothDevice bleDevice, int rssi, byte[] scanRecord)
		{
			Trace.Message("Adapter.LeScanCallback: " + bleDevice?.Name);
			var device = new Device(adapter, bleDevice, null, scanRecord);
			device.Rssi.Timestamp = DateTime.Now;
			device.Rssi.Value = rssi is < 0 and >= sbyte.MinValue ? (sbyte)rssi : default;
			adapter.HandleDiscoveredDevice(device); // No IsConnectable!
		}
	}

	public class Api21BleScanCallback(Adapter adapter) : ScanCallback
	{
		public override void OnScanFailed(ScanFailure errorCode)
		{
			Trace.Message("Adapter: Scan failed with code {0}", errorCode);
			base.OnScanFailed(errorCode);
		}

		public override void OnScanResult(ScanCallbackType callbackType, ScanResult result)
		{
			if (result?.Device is null)
			{
				Trace.Message($"scan result or its device return null");
				return;
			}

			base.OnScanResult(callbackType, result);

			/* Might want to transition to parsing the API21+ ScanResult, but sort of a pain for now
			List<AdvertisementRecord> records = new List<AdvertisementRecord>();
			records.Add(new AdvertisementRecord(AdvertisementRecordType.Flags, BitConverter.GetBytes(result.ScanRecord.AdvertiseFlags)));
			if (!string.IsNullOrEmpty(result.ScanRecord.DeviceName))
			{
				records.Add(new AdvertisementRecord(AdvertisementRecordType.CompleteLocalName, Encoding.UTF8.GetBytes(result.ScanRecord.DeviceName)));
			}
			for (int i = 0; i < result.ScanRecord.ManufacturerSpecificData.Size(); i++)
			{
				int key = result.ScanRecord.ManufacturerSpecificData.KeyAt(i);
				var arr = result.ScanRecord.GetManufacturerSpecificData(key);
				byte[] data = new byte[arr.Length + 2];
				BitConverter.GetBytes((ushort)key).CopyTo(data,0);
				arr.CopyTo(data, 2);
				records.Add(new AdvertisementRecord(AdvertisementRecordType.ManufacturerSpecificData, data));
			}

			foreach(var uuid in result.ScanRecord.ServiceUuids)
			{
				records.Add(new AdvertisementRecord(AdvertisementRecordType.UuidsIncomplete128Bit, uuid.Uuid.));
			}

			foreach(var key in result.ScanRecord.ServiceData.Keys)
			{
				records.Add(new AdvertisementRecord(AdvertisementRecordType.ServiceData, result.ScanRecord.ServiceData));
			}*/

			#if NET6_0_OR_GREATER
			var device = new Device(adapter, result.Device, null, result.ScanRecord?.GetBytes(), !OperatingSystem.IsAndroidVersionAtLeast(26) || result.IsConnectable);
			#else
			var device = new Device(adapter, result.Device, null, result.ScanRecord?.GetBytes(), Build.VERSION.SdkInt < BuildVersionCodes.O || result.IsConnectable);
			#endif
			device.Rssi.Timestamp = DateTime.Now;
			device.Rssi.Value = result.Rssi is < 0 and >= sbyte.MinValue ? (sbyte)result.Rssi : default;
			//Device device;
			//if (result.ScanRecord.ManufacturerSpecificData.Size() > 0)
			//{
			//    int key = result.ScanRecord.ManufacturerSpecificData.KeyAt(0);
			//    byte[] mdata = result.ScanRecord.GetManufacturerSpecificData(key);
			//    byte[] mdataWithKey = new byte[mdata.Length + 2];
			//    BitConverter.GetBytes((ushort)key).CopyTo(mdataWithKey, 0);
			//    mdata.CopyTo(mdataWithKey, 2);
			//    device = new Device(result.Device, null, null, result.Rssi, mdataWithKey);
			//}
			//else
			//{
			//    device = new Device(result.Device, null, null, result.Rssi, new byte[0]);
			//}

			adapter.HandleDiscoveredDevice(device);
		}
	}
}