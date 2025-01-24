using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using Plugin.BLE.Abstractions.Utils;


// ReSharper disable once CheckNamespace
namespace Plugin.BLE.Abstractions;

/// <summary>
/// Base class for all platform-specific Adapter classes.
/// </summary>
public abstract class AdapterBase : IAdapter
{
	private CancellationTokenSource _scanCancellationTokenSource;
	private volatile bool _isScanning;
	private Func<IDevice, bool> _currentScanDeviceFilter;

	#region Discovery

	/// <summary>
	/// Occurs when the scan has been stopped due the timeout after <see cref="ScanTimeout"/> ms.
	/// </summary>
	public event EventHandler ScanTimeoutElapsed;

	/// <summary>
	/// Occurs when the adapter receives an advertisement for the first time of the current scan run.
	/// This means once per every <c>StartScanningForDevicesAsync</c> call.
	/// </summary>
	public event EventHandler<DeviceEventArgs> DeviceDiscovered;

	/// <summary>
	/// Occurs when the adapter receives an advertisement.
	/// </summary>
	public event EventHandler<DeviceEventArgs> DeviceAdvertised;

	/// <summary>
	/// Indicates, if the adapter is scanning for devices.
	/// </summary>
	public bool IsScanning
	{
		get => _isScanning;
		private set => _isScanning = value;
	}

	/// <summary>
	/// Timeout for Ble scanning. Default is 10000.
	/// </summary>
	public int ScanTimeout { get; set; } = 10000;

	/// <summary>
	/// Specifies the scanning mode. Must be set before calling StartScanningForDevicesAsync().
	/// Changing it while scanning, will have no change the current scan behavior.
	/// Default: <see cref="ScanMode.LowPower"/>
	/// </summary>
	public ScanMode ScanMode { get; set; } = ScanMode.LowPower;

	/// <summary>
	/// Scan match mode defines how aggressively we look for adverts
	/// </summary>
	public ScanMatchMode ScanMatchMode { get; set; } = ScanMatchMode.STICKY;

	/// <summary>
	/// Starts scanning for BLE devices that fulfill the <paramref name="deviceFilter"/>.
	/// DeviceDiscovered will only be called, if <paramref name="deviceFilter"/> returns <c>true</c> for the discovered device.
	/// </summary>
	public async Task StartScanningForDevicesAsync(ScanFilterOptions scanFilterOptions, Func<IDevice, bool> deviceFilter = null, bool allowDuplicatesKey = false, CancellationToken cancellationToken = default)
	{
		if (IsScanning)
		{
			Trace.Message("Adapter: Already scanning!");
			return;
		}

		IsScanning = true;
		_currentScanDeviceFilter = deviceFilter ?? (_ => true);
		_scanCancellationTokenSource = new();

		try
		{
			DiscoveredDevicesRegistry.Clear();

			using (cancellationToken.Register(() => _scanCancellationTokenSource?.Cancel()))
			{
				await StartScanningForDevicesNativeAsync(scanFilterOptions, allowDuplicatesKey, _scanCancellationTokenSource.Token);
				await Task.Delay(ScanTimeout, _scanCancellationTokenSource.Token);
				Trace.Message("Adapter: Scan timeout has elapsed.");
				CleanupScan();
				ScanTimeoutElapsed?.Invoke(this, System.EventArgs.Empty);
			}
		}
		catch (TaskCanceledException)
		{
			CleanupScan();
			Trace.Message("Adapter: Scan was cancelled.");
		}
	}

	/// <summary>
	/// Starts scanning for BLE devices that fulfill the <paramref name="deviceFilter"/>.
	/// DeviceDiscovered will only be called, if <paramref name="deviceFilter"/> returns <c>true</c> for the discovered device.
	/// This overload takes a list of service IDs and is only kept for backwards compatibility. Might be removed in a future version.
	/// </summary>
	public async Task StartScanningForDevicesAsync(Guid[] serviceUuids, Func<IDevice, bool> deviceFilter = null, bool allowDuplicatesKey = false, CancellationToken cancellationToken = default)
		=> await StartScanningForDevicesAsync(new ScanFilterOptions { ServiceUuids = serviceUuids }, deviceFilter, allowDuplicatesKey, cancellationToken);

	/// <summary>
	/// Stops scanning for BLE devices.
	/// </summary>
	public Task StopScanningForDevicesAsync()
	{
		if (_scanCancellationTokenSource != null && !_scanCancellationTokenSource.IsCancellationRequested)
			_scanCancellationTokenSource.Cancel();
		else
			Trace.Message("Adapter: Already cancelled scan.");

		return Task.FromResult(0);
	}

	/// <summary>
	/// Dictionary of all discovered devices, indexed by Guid.
	/// </summary>
	private ConcurrentDictionary<Guid, IDevice> DiscoveredDevicesRegistry { get; } = new();

	/// <summary>
	/// List of all discovered devices.
	/// </summary>
	public virtual IReadOnlyList<IDevice> DiscoveredDevices => DiscoveredDevicesRegistry.Values.ToList();

	/// <summary>
	/// Indicates whether extended advertising (BLE5) is supported.
	/// </summary>
	public virtual bool SupportsExtendedAdvertising() => false;

	/// <summary>
	/// Handle discovery of a new device.
	/// </summary>
	protected void HandleDiscoveredDevice(IDevice device)
	{
		if (_currentScanDeviceFilter != null && !_currentScanDeviceFilter(device))
			return;

		DeviceAdvertised?.Invoke(this, new(device));

		// TODO (sms): check equality implementation of device
		if (DiscoveredDevicesRegistry.TryAdd(device.Id, device))
			DeviceDiscovered?.Invoke(this, new(device));
	}

	/// <summary>
	/// Native implementation of StartScanningForDevicesAsync.
	/// </summary>
	protected abstract Task StartScanningForDevicesNativeAsync(ScanFilterOptions scanFilterOptions, bool allowDuplicatesKey, CancellationToken scanCancellationToken);

	/// <summary>
	/// Stopping the scan (native implementation).
	/// </summary>
	protected abstract void StopScanNative();

	private void CleanupScan()
	{
		Trace.Message("Adapter: Stopping the scan for devices.");
		StopScanNative();

		if (_scanCancellationTokenSource != null)
		{
			_scanCancellationTokenSource.Dispose();
			_scanCancellationTokenSource = null;
		}

		IsScanning = false;
	}

	#endregion Discovery

	#region Connection

	/// <summary>
	/// Occurs when a device has been connected.
	/// </summary>
	public event EventHandler<DeviceEventArgs> DeviceConnected;

	/// <summary>
	/// Occurs when a device has been disconnected. This occurs on intended disconnects after <see cref="DisconnectDeviceAsync"/>.
	/// </summary>
	public event EventHandler<DeviceEventArgs> DeviceDisconnected;

	/// <summary>
	/// Occurs when a device has been disconnected. This occurs on unintended disconnects (e.g. when the device exploded).
	/// </summary>
	public event EventHandler<DeviceErrorEventArgs> DeviceConnectionLost;

	/// <summary>
	/// Occurs when the connection to a device fails.
	/// </summary>
	public event EventHandler<DeviceErrorEventArgs> DeviceConnectionError;

	/// <summary>
	/// List of all connected devices.
	/// </summary>
	public virtual IReadOnlyList<IDevice> ConnectedDevices => ConnectedDeviceRegistry.Values.ToList();

	/// <summary>
	/// Used to store all connected devices
	/// </summary>
	protected internal ConcurrentDictionary<string, IDevice> ConnectedDeviceRegistry { get; } = new();

	/// <summary>
	/// Connects to the <paramref name="device"/>.
	/// </summary>
	/// <param name="device">Device to connect to.</param>
	/// <param name="connectParameters">Connection parameters. Contains platform specific parameters needed to achieved connection. The default value is None.</param>
	/// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is None.</param>
	/// <returns>A task that represents the asynchronous read operation. The Task will finish after the device has been connected successfully.</returns>
	public async Task ConnectToDeviceAsync(IDevice device, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default)
	{
		if (device == null)
			throw new ArgumentNullException(nameof(device));

		if (device.State == DeviceState.Connected)
			return;

		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
		{
			await TaskBuilder.FromEvent<bool, EventHandler<DeviceEventArgs>, EventHandler<DeviceErrorEventArgs>>
			(
				execute: () => ConnectToDeviceNativeAsync(device, connectParameters, cts.Token),
				// ReSharper disable once UnusedParameter.Local
				getCompleteHandler: (complete, reject) => (_, args) =>
				{
					if (args.Device.Id == device.Id)
					{
						Trace.Message($"{nameof(ConnectToDeviceAsync)} Connected: {args.Device.Id} {args.Device.Name}");
						complete(true);
					}
				},
				subscribeComplete: handler => DeviceConnected += handler,
				unsubscribeComplete: handler => DeviceConnected -= handler,
				getRejectHandler: reject => (_, args) =>
				{
					if (args?.Device != null && args.Device.Id == device.Id)
					{
						Trace.Message($"{nameof(ConnectToDeviceAsync)} Error: {args.Device.Id} {args.Device.Name}");
						reject(new DeviceConnectionException(args.Device.Id, args.Device?.Name, args.ErrorMessage));
					}
				},
				subscribeReject: handler => DeviceConnectionError += handler,
				unsubscribeReject: handler => DeviceConnectionError -= handler,
				token: cts.Token, mainThread: false
			);
		}
	}

	/// <summary>
	/// Connects to a device with a known GUID without scanning and if in range. Does not scan for devices.
	/// </summary>
	public async Task<IDevice> ConnectToKnownDeviceAsync(Guid deviceGuid, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default)
	{
		if (DiscoveredDevicesRegistry.TryGetValue(deviceGuid, out var discoveredDevice))
		{
			await ConnectToDeviceAsync(discoveredDevice, connectParameters, cancellationToken);
			return discoveredDevice;
		}

		var connectedDevice = await ConnectToKnownDeviceNativeAsync(deviceGuid, connectParameters, cancellationToken);
		if (!DiscoveredDevicesRegistry.ContainsKey(deviceGuid))
			DiscoveredDevicesRegistry.TryAdd(deviceGuid, connectedDevice);

		return connectedDevice;
	}

	/// <summary>
	/// Disconnects from the <paramref name="device"/>.
	/// </summary>
	/// <param name="device">Device to connect from.</param>
	/// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is None.</param>
	public Task DisconnectDeviceAsync(IDevice device, CancellationToken cancellationToken = default)
	{
		if (!ConnectedDevices.Contains(device))
		{
			Trace.Message($"{nameof(DisconnectDeviceAsync)}: device {device.Name} not in the list of connected devices.");
			return Task.FromResult(false);
		}

		return TaskBuilder.FromEvent<bool, EventHandler<DeviceEventArgs>, EventHandler<DeviceErrorEventArgs>>
		(
			execute: () => DisconnectDeviceNative(device),
			getCompleteHandler: (complete, reject) => (_, args) =>
			{
				if (args.Device.Id == device.Id)
				{
					Trace.Message($"{nameof(DisconnectDeviceAsync)} Disconnected: {args.Device.Id} {args.Device.Name}");
					complete(true);
				}
			},
			subscribeComplete: handler => DeviceDisconnected += handler,
			unsubscribeComplete: handler => DeviceDisconnected -= handler,
			getRejectHandler: reject => (sender, args) =>
			{
				if (args.Device.Id == device.Id)
				{
					Trace.Message($"{nameof(DisconnectDeviceAsync)}, Disconnect Error: {args.Device?.Id} {args.Device?.Name}");
					reject(new("Disconnect operation exception"));
				}
			},
			subscribeReject: handler => DeviceConnectionError += handler,
			unsubscribeReject: handler => DeviceConnectionError -= handler,
			token: cancellationToken
		);
	}

	/// <summary>
	/// Native implementation of ConnectToDeviceAsync.
	/// </summary>
	protected abstract Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters, CancellationToken cancellationToken);

	/// <summary>
	/// Native implementation of DisconnectDeviceAsync.
	/// </summary>
	protected abstract void DisconnectDeviceNative(IDevice device);

	/// <summary>
	/// Native implementation of ConnectToKnownDeviceAsync.
	/// </summary>
	protected abstract Task<IDevice> ConnectToKnownDeviceNativeAsync(Guid deviceGuid, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default);

	/// <summary>
	/// Handle connection of a new device.
	/// </summary>
	protected internal void HandleConnectedDevice(IDevice device) => DeviceConnected?.Invoke(this, new(device));

	/// <summary>
	/// Handle disconnection of a device.
	/// </summary>
	protected internal void HandleDisconnectedDevice(bool disconnectRequested, IDevice device, string message = "")
	{
		if (disconnectRequested)
		{
			Trace.Message($"DisconnectedPeripheral by user: {device?.Name}");
			DeviceDisconnected?.Invoke(this, new(device));
		}
		else
		{
			var m = !string.IsNullOrWhiteSpace(message) ? message : "DisconnectedPeripheral by lost signal";
			Trace.Message($"{m}: {device.Name}");
			DeviceConnectionLost?.Invoke(this, new(device, m));

			if (DiscoveredDevicesRegistry.TryRemove(device.Id, out _))
				Trace.Message($"Removed device from discovered devices list: {device.Name}");
		}
	}

	/// <summary>
	/// Handle connection failure.
	/// </summary>
	protected internal void HandleConnectionFail(IDevice device, string errorMessage)
	{
		Trace.Message($"Failed to connect peripheral {device.Id}: {device.Name}");
		DeviceConnectionError?.Invoke(this, new(device, errorMessage));
	}

	#endregion Connection

	#region Connect + Bond

	/// <summary>
	/// Returns all BLE devices connected to the system.
	/// </summary>
	public abstract IReadOnlyList<IDevice> GetConnectedOrBondedDevices(Guid[] services = null);

	/// <summary>
	/// Returns a list of paired BLE devices for the given UUIDs.
	/// </summary>
	public abstract IReadOnlyList<IDevice> GetConnectedOrBondedDevicesByIds(Guid[] ids);

	#endregion Connect + Bond

	/// <summary>
	/// Indicates whether the Coded PHY feature (BLE5) is supported.
	/// </summary>
	public virtual bool SupportsCodedPhy() => false;
}