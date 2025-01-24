using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Extensions;


namespace Plugin.BLE.Windows;

internal class BluetoothLeDeviceManager
{
	private readonly DeviceWatcher _pairedWatcher = DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
	private readonly DeviceWatcher _unPairedWatcher = DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(false));
	private readonly DeviceWatcher _connectedWatcher = DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
	private readonly ConcurrentDictionary<string, (DeviceInformation DeviceInfo, DateTime? RemovedTime)> _cachedDevices = new();
	private bool _cachePositiveEnumerationComplete, _cacheNegativeEnumerationComplete;
	private readonly SemaphoreSlim _semaphore = new(1, 1);
	private readonly Timer _checkTimer;
	private static readonly TimeSpan _inversionTransitionPeriod = TimeSpan.FromSeconds(15); // should be at least the time it takes to be removed from one list and added to the other, and as long as you want Devices to be cached
	private readonly Dictionary<string, UpdateableProperties> _propertyMap = new()
	{
		{ "System.ItemNameDisplay", UpdateableProperties.Name },
		{ "System.Devices.Icon", UpdateableProperties.Icon },
		{ "System.Devices.GlyphIcon", UpdateableProperties.GlyphIcon }
	};

	protected internal BluetoothLeDeviceManager()
	{
		_checkTimer = new(CacheRemovalCheck);
		CreateCacheWatcher(_pairedWatcher, _unPairedWatcher);

		_pairedWatcher.Added += (_, deviceInformation) => DeviceUpdated?.Invoke(this, new BleDevicePairedEventArgs(deviceInformation));
		_pairedWatcher.Removed += (_, deviceInformationUpdate) => DeviceUpdated?.Invoke(this, new BleDeviceUnpairedEventArgs(deviceInformationUpdate.Id));
		_connectedWatcher.Added += (_, deviceInformation) => DeviceUpdated?.Invoke(this, new BleDeviceConnectedEventArgs(deviceInformation));
		_connectedWatcher.Removed += (_, deviceInformationUpdate) => DeviceUpdated?.Invoke(this, new BleDeviceDisconnectedEventArgs(deviceInformationUpdate.Id));

		_pairedWatcher.Start();
		_unPairedWatcher.Start();
		_connectedWatcher.Start();
	}

	internal event EventHandler<BleDeviceManagerEventArgs> DeviceUpdated;

	internal enum UpdateableProperties
	{
		Name,
		Icon,
		GlyphIcon
	}

	internal IReadOnlyList<DeviceInformation> GetCachedDevices()
	{
		_semaphore.Wait();
		try { return _cachedDevices.Values.Select(static cachedDevices => cachedDevices.DeviceInfo).ToList(); }
		finally { _semaphore.Release(); }
	}

	private void CreateCacheWatcher(DeviceWatcher positiveWatcher, DeviceWatcher negativeWatcher)
	{
		Configure(positiveWatcher);
		Configure(negativeWatcher);

		void Configure(DeviceWatcher watcher)
		{
			watcher.Added += async (_, deviceInfo) =>
			{
				await _semaphore.WaitAsync();
				try { _cachedDevices.AddOrUpdate(deviceInfo.Id, (deviceInfo, null), (_, _) => (deviceInfo, null)); }
				finally { _semaphore.Release(); }
			};
			watcher.Removed += async (_, deviceInfoUpdate) =>
			{
				await _semaphore.WaitAsync();
				try { _cachedDevices.AddOrUpdate(deviceInfoUpdate.Id, (null, DateTime.Now), static (_, existingValue) => (existingValue.DeviceInfo, DateTime.Now)); }
				finally { _semaphore.Release(); }
			};
			watcher.Updated += (_, deviceInformationUpdate) =>
			{
				var updatedProperties = new Dictionary<UpdateableProperties, string>();
				foreach (var property in deviceInformationUpdate.Properties)
				{
					Trace.Message($"update prop: {property.Key} - {property.Value}");
					if (_propertyMap.TryGetValue(property.Key, out var propType))
						updatedProperties[propType] = property.Value as string;
				}

				DeviceUpdated?.Invoke(this, new BleDeviceUpdatedEventArgs(deviceInformationUpdate.Id, updatedProperties));
			};

			watcher.EnumerationCompleted += (sender, _) =>
			{
				if (sender == _pairedWatcher)
					_cachePositiveEnumerationComplete = true;
				else if (sender == _unPairedWatcher)
					_cacheNegativeEnumerationComplete = true;

				if (_cachePositiveEnumerationComplete && _cacheNegativeEnumerationComplete)
				{
					_checkTimer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
					Trace.Message($"Device caches ready monitor start, count: {_cachedDevices.Count}");
				}
			};
		}
	}

	private void CacheRemovalCheck(object _)
	{
		List<string> devicesToConfirmAsRemoved = [];

		_semaphore.Wait();
		try
		{
			devicesToConfirmAsRemoved.AddRange(from entry in _cachedDevices where entry.Value.RemovedTime.HasValue && (DateTime.Now - entry.Value.RemovedTime.Value) > _inversionTransitionPeriod select entry.Key);
			foreach (var deviceId in devicesToConfirmAsRemoved)
			{
				_cachedDevices.TryRemove(deviceId, out var _);
				DeviceUpdated?.Invoke(this, new BleDeviceRemovedFromCacheEventArgs(deviceId)); Trace.Message($"Removed from cache: {deviceId.ToBleDeviceGuidFromId()} {_cachedDevices.Count} remain");
			}
		}
		finally
		{
			_semaphore.Release();
			_checkTimer.Change(_inversionTransitionPeriod, Timeout.InfiniteTimeSpan);
		}
	}


	internal abstract class BleDeviceManagerEventArgs(string id) : EventArgs
	{
		internal DateTime TimeStamp { get; } = DateTime.Now;
		internal string Id { get; } = id;
	}

	internal abstract class BleDeviceDeviceInformation(DeviceInformation deviceInformation) : BleDeviceManagerEventArgs(deviceInformation.Id)
	{
		internal DeviceInformation DeviceInformation { get; } = deviceInformation;
	}

	internal class BleDeviceAddedToCacheEventArgs(DeviceInformation deviceInformation) : BleDeviceDeviceInformation(deviceInformation);
	internal class BleDeviceRemovedFromCacheEventArgs(string id) : BleDeviceManagerEventArgs(id);
	internal class BleDevicePairedEventArgs(DeviceInformation deviceInformation) : BleDeviceDeviceInformation(deviceInformation);
	internal class BleDeviceUnpairedEventArgs(string id) : BleDeviceManagerEventArgs(id);
	internal class BleDeviceConnectedEventArgs(DeviceInformation deviceInformation) : BleDeviceDeviceInformation(deviceInformation);
	internal class BleDeviceDisconnectedEventArgs(string id) : BleDeviceManagerEventArgs(id);

	internal class BleDeviceUpdatedEventArgs(string id, Dictionary<UpdateableProperties, string> properties) : BleDeviceManagerEventArgs(id)
	{
		internal Dictionary<UpdateableProperties, string> Properties { get; } = properties;
	}
}
