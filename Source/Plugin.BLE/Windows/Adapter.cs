using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Extensions;
using Plugin.BLE.Shared.Contracts.Connection;
using Plugin.BLE.Shared.Contracts.Connection.Adapter;
using Plugin.BLE.Shared.Contracts.Connection.Device;
using Plugin.BLE.Shared.Contracts.Pairing;
using Plugin.BLE.Shared.Contracts.Pairing.Adapter;
using Plugin.BLE.Shared.Contracts.RequestResults;

using static Plugin.BLE.Windows.BluetoothLeDeviceManager;


namespace Plugin.BLE.Windows;

public class Adapter : AdapterBase, IBondReport, IBondRequest, IPairProcess, IConnectReport, IConnectRequest
{
	private readonly BluetoothAdapter _adapter;
	private BluetoothLEAdvertisementWatcher _bluetoothLeAdvertisementWatcher;

	private static readonly SafeCreateDictionary<Guid, Device> MasterDevices = new();

	public Adapter(BluetoothAdapter adapter)
	{
		_adapter = adapter;

		var bluetoothLeDeviceManager = new BluetoothLeDeviceManager();
		bluetoothLeDeviceManager.DeviceUpdated += async (_, bleDeviceManagerEventArgs) =>
		{
			var id = bleDeviceManagerEventArgs.Id.ToBleDeviceGuidFromId();

			switch (bleDeviceManagerEventArgs)
			{
				case BleDeviceRemovedFromCacheEventArgs:
					if (MasterDevices.TryGetValue(id, out var removedDevice) && !removedDevice.IsBonded && !removedDevice.IsConnected) // :( a removed device is likely not Bonded or Connected
						MasterDevices.TryRemove(id, out var validRemovedDevice);
					break;
				case BleDeviceConnectedEventArgs bleDeviceConnectedEventArgs:
					var connectedDevice = await MasterDevices.AddOrUpdate(id, valueCreator: () => new(this, id, bleDeviceConnectedEventArgs.DeviceInformation.Name, true),
						valueUpdater: async existing =>
						{
							existing.IsConnected = true; // must assume an already connected device is connectable
							existing.CanPair = bleDeviceConnectedEventArgs.DeviceInformation.Pairing.CanPair;
							existing.Name = bleDeviceConnectedEventArgs.DeviceInformation.Name; // theoretically this could have been updated?
							await existing.VerifyUnderlyingDevice();
							return existing;
						}
					);
					DeviceConnectionStateChanged?.Invoke(this, new(connectedDevice));
					//HandleConnectedDevice(connectedDevice); // ToDo eliminate
					break;
				case BleDeviceDisconnectedEventArgs:
					if (MasterDevices.TryGetValue(id, out var disconnectedDevice))
					{
						disconnectedDevice.IsConnected = false;
						DeviceConnectionStateChanged?.Invoke(this, new(disconnectedDevice)); // ToDo deal with lost connections later
						//HandleDisconnectedDevice(true, disconnectedDevice); // ToDo eliminate
					}
					break;
				case BleDevicePairedEventArgs bleDevicePairedEventArgs:
					var pairedDevice = await MasterDevices.AddOrUpdate(id, valueCreator: () => new(this, id, bleDevicePairedEventArgs.DeviceInformation.Name, true),
						valueUpdater: async existing =>
						{
							existing.IsBonded = true;
							existing.CanPair = true; // deviceInformation.Pairing.CanPair; // must assume an already paired device is pairable
							existing.Name = bleDevicePairedEventArgs.DeviceInformation.Name; // theoretically this could have been updated?
							await existing.VerifyUnderlyingDevice();
							return existing;
						}
					);
					pairedDevice.IsBonded = true;
					DeviceBondStateChanged?.Invoke(this, new(pairedDevice, id.ToBleAddress().ToHexBleAddress(), DeviceBondState.Bonded));
					break;
				case BleDeviceUnpairedEventArgs:
					if (MasterDevices.TryGetValue(id, out var unpairedDevice))
					{
						unpairedDevice.IsBonded = false;
						DeviceBondStateChanged?.Invoke(this, new(unpairedDevice, id.ToBleAddress().ToHexBleAddress(), DeviceBondState.NotBonded));
					}
					break;
				case BleDeviceUpdatedEventArgs bleDeviceUpdatedEventArgs:
					if (MasterDevices.TryGetValue(id, out var updatedDevice))
					{
						if (bleDeviceUpdatedEventArgs.Properties.TryGetValue(UpdateableProperties.Name, out var name))
							updatedDevice.Name = name;
					}
					break;
			}
		};
	}

	#region Discovery

	protected override Task StartScanningForDevicesNativeAsync(ScanFilterOptions scanFilterOptions, bool allowDuplicatesKey, CancellationToken scanCancellationToken)
	{
		if (Environment.OSVersion.Version >= new Version(10, 0, 17763, 0))
			_bluetoothLeAdvertisementWatcher = new() { AllowExtendedAdvertisements = true, ScanningMode = ScanMode.ToNative() };
		else
			_bluetoothLeAdvertisementWatcher = new() { ScanningMode = ScanMode.ToNative() };

		if (scanFilterOptions?.HasServiceIds == true)
			foreach (var uuid in scanFilterOptions.ServiceUuids)
				_bluetoothLeAdvertisementWatcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(uuid);

		if (scanFilterOptions?.HasDeviceNames == true)
			_bluetoothLeAdvertisementWatcher.AdvertisementFilter.Advertisement.LocalName = scanFilterOptions.DeviceNames.First();

		if (scanFilterOptions?.HasManufacturerIds == true)
			foreach (var manufacturerDataFilter in scanFilterOptions.ManufacturerDataFilters)
				_bluetoothLeAdvertisementWatcher.AdvertisementFilter.Advertisement.ManufacturerData.Add
				(
					new()
					{
						CompanyId = manufacturerDataFilter.ManufacturerId,
						Data = ConvertToIBuffer(manufacturerDataFilter.ManufacturerData)
					}
				);

		_bluetoothLeAdvertisementWatcher.Received += OnAdvertisementReceived;
		_bluetoothLeAdvertisementWatcher.Start();
		return Task.FromResult(true);

		static IBuffer ConvertToIBuffer(byte[] byteArray)
		{
			using var dataWriter = new DataWriter();
			dataWriter.WriteBytes(byteArray);
			return dataWriter.DetachBuffer();
		}
	}

	protected override void StopScanNative()
	{
		if (_bluetoothLeAdvertisementWatcher != null)
		{
			_bluetoothLeAdvertisementWatcher.Stop();
			_bluetoothLeAdvertisementWatcher.Received -= OnAdvertisementReceived;
			_bluetoothLeAdvertisementWatcher = null;
		}
	}

	private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs advertisement) => _ = OnAdvertisementReceivedAsync(watcher, advertisement);

	private async Task OnAdvertisementReceivedAsync(BluetoothLEAdvertisementWatcher _, BluetoothLEAdvertisementReceivedEventArgs advertisement)
	{
		try // don't let event handlers throw
		{
			var id = advertisement.BluetoothAddress.ParseDeviceId();
			var device = await MasterDevices.AddOrUpdate(id, valueCreator: () => new(this, id, advertisement.Advertisement.LocalName, advertisement.IsConnectable || AdvertTypeConnectable()),
				valueUpdater: async existing =>
				{
					existing.Rssi.Timestamp = advertisement.Timestamp.LocalDateTime;
					existing.Rssi.Value = advertisement.RawSignalStrengthInDBm is < 0 and >= sbyte.MinValue ? (sbyte)advertisement.RawSignalStrengthInDBm : default;
					existing.AdvertisementRecords = ParseAdvertisementData(advertisement.Advertisement);
					await existing.VerifyUnderlyingDevice();
					return existing;
				}
			);
			HandleDiscoveredDevice(device);

			bool AdvertTypeConnectable() => advertisement.AdvertisementType is BluetoothLEAdvertisementType.ConnectableUndirected or BluetoothLEAdvertisementType.ConnectableDirected;
		}
		catch (Exception ex) { Trace.Message($"{nameof(OnAdvertisementReceivedAsync)} {ex.Message}"); }

	}

	/// <summary>
	/// Parses a given advertisement for various stored properties
	/// Currently only parses the manufacturer specific data
	/// </summary>
	/// <param name="ad">The advertisement to parse</param>
	/// <returns>List of generic advertisement records</returns>
	public static List<AdvertisementRecord> ParseAdvertisementData(BluetoothLEAdvertisement ad) => ad.DataSections.Select(static data => new AdvertisementRecord((AdvertisementRecordType)data.DataType, data.Data?.ToArray())).ToList();

	public override bool SupportsExtendedAdvertising() => _adapter.IsExtendedAdvertisingSupported;

	#endregion Discovery

	#region Connection

	protected override async Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters, CancellationToken cancellationToken) => await ((Device)device).ConnectInternal(connectParameters, cancellationToken);

	protected override async Task<IDevice> ConnectToKnownDeviceNativeAsync(Guid deviceGuid, ConnectParameters connectParameters, CancellationToken cancellationToken) // ReSharper restore OptionalParameterHierarchyMismatch
	{
		try
		{
			var device = MasterDevices.GetOrAdd(deviceGuid, () => new(this, deviceGuid, "", true));
			if (device.IsConnectable)
				await ConnectToDeviceAsync(device, connectParameters, cancellationToken);

			return device;
		}
		catch (Exception e)
		{
			Trace.Message(e.Message);
			throw;
		}
	}

	protected override void DisconnectDeviceNative(IDevice device)
	{
		Trace.Message($"{nameof(DisconnectDeviceNative)} from device ID: {device.Id.ToHexBleAddress()}");
		((Device)device).DisconnectInternal();
	}

	public virtual async Task<IResult> ConnectToDeviceAsyncX(IDevice device, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(device);

		if (device.State == DeviceState.Connected)
			return new ConnectionResult(ResultStatus.SpecifiedFailure, "Device already Connected", device);

		return await ((IConnectProcess)device).ConnectInternalX(connectParameters, cancellationToken).ConfigureAwait(false);
	}

	#endregion Connection

	#region Implementation of IConnectReport

	public event EventHandler<DeviceConnectionChangedEventArgs> DeviceConnectionStateChanged;

	IReadOnlyList<IDevice> IConnectReport.ConnectedDevices => MasterDevices.Values.Where(static device => device.IsConnected).ToList();

	#endregion

	#region Implementation of IConnectRequest

	public Task<IResult> ConnectToDevice(IDevice device, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default) => throw new NotImplementedException();

	public Task<IResult> ConnectToDeviceById(Guid id, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default) => throw new NotImplementedException();

	public Task<IResult> DisconnectDevice(IDevice device, CancellationToken cancellationToken = default) => throw new NotImplementedException();

	#endregion

	#region Connection Bonding

	public override IReadOnlyList<IDevice> GetConnectedOrBondedDevices(Guid[] services = null) => MasterDevices.Values.Where(static device => device.IsBonded || device.BondState == DeviceBondState.Bonded).ToList();

	public override IReadOnlyList<IDevice> GetConnectedOrBondedDevicesByIds(Guid[] ids) => GetConnectedOrBondedDevices().Where(device => ids.Contains(device.Id)).ToList();

	#endregion Connection Bonding

	#region Implementation of IBondReport

	public event EventHandler<DeviceBondStateChangedEventArgs> DeviceBondStateChanged;

	public IReadOnlyList<IDevice> BondedDevices => MasterDevices.Values.Where(static device => device.IsBonded).ToList();

	#endregion

	#region Implementation of IBondRequest

	public async Task<IResult> Bond(IDevice device, BondingOptions options, CancellationToken cancellationToken)
	{
		// ToDo: should these return a failed result instead?
		ArgumentNullException.ThrowIfNull(device);

		if (!device.IsConnectable)
			return new BondResult(ResultStatus.SpecifiedFailure, "Non connectable devices cannot bond");

		// at this point it's a once paired or connected device, there should be a device id // ToDo Remove if a non issue
		var deviceId = ((Device)device).DeviceId ??= ((Device)device).NativeDevice?.DeviceId;
		if (string.IsNullOrWhiteSpace(deviceId))
			throw new ArgumentException($"Invalid argument {nameof(Device.DeviceId)}", nameof(device));

		DeviceInformation deviceInformation = null;
		try
		{
			deviceInformation = await DeviceInformation.CreateFromIdAsync(deviceId).AsTask(cancellationToken);

			if (deviceInformation.Pairing.IsPaired)
			{
				const DevicePairingResultStatus status = DevicePairingResultStatus.AlreadyPaired;
				return new BondResultManualPair(status.XPlatformPairStatus(), $"{status}", deviceInformation.Pairing.ProtectionLevel.XPlatformProtectionLevel());
			}

			if (!deviceInformation.Pairing.CanPair)
			{
				const DevicePairingResultStatus status = DevicePairingResultStatus.NotReadyToPair;
				return new BondResultManualPair(status.XPlatformPairStatus(), $"{status}", deviceInformation.Pairing.ProtectionLevel.XPlatformProtectionLevel());
			}

			cancellationToken.ThrowIfCancellationRequested(); // check for cancel after allowing it to use the awaited to exit gracefully, but before subsequent awaited code

			DevicePairingResult result;
			if (options == null)
				result = await deviceInformation.Pairing.PairAsync().AsTask(cancellationToken);
			else
			{
				deviceInformation.Pairing.Custom.PairingRequested += OnPairingRequested;
				var requestedModes = (DevicePairingKinds)options.RequestedModes;
				var requestedProtection = (DevicePairingProtectionLevel)options.MinimumRequestedProtection;
				result = await deviceInformation.Pairing.Custom.PairAsync(requestedModes, requestedProtection).AsTask(cancellationToken);
			}

			Trace.Message($"Pairing {nameof(result)}: {result.Status}");
			return new BondResultManualPair(result.Status.XPlatformPairStatus(), $"{result.Status}", (ProtectionLevel)result.ProtectionLevelUsed);
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
						args.AcceptWithPasswordCredential(new(credentialsResponse.Resource, credentialsResponse.UserName, credentialsResponse.Password));
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
