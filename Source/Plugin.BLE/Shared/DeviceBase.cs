using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Shared.Contracts.Rssi;
using Plugin.BLE.Shared.Utils;


namespace Plugin.BLE.Abstractions;

public abstract class DeviceBase<TNativeDevice> : IDevice, ICancellationMaster
{
	private readonly IAdapter _adapter;
	private readonly List<IService> _knownServices = [];

	protected DeviceBase(IAdapter adapter, TNativeDevice nativeDevice)
	{
		_adapter = adapter;
		NativeDevice = nativeDevice;
	}

	protected DeviceBase(IAdapter adapter, bool isConnectable)
	{
		_adapter = adapter;
		IsConnectable = isConnectable;
	}

	CancellationTokenSource ICancellationMaster.TokenSource { get; set; } = new();

	#region Basic

	/// <summary>
	/// ID of the device.
	/// </summary>
	public Guid Id { get; protected set; }

	/// <summary>
	/// Name of the Device acquired from advertisement or connection
	/// </summary>
	public virtual string Name { get; protected internal set; }

	#endregion Basic

	#region Advertisments

	/// <summary>
	/// All the advertisement records.
	/// </summary>
	public IReadOnlyList<AdvertisementRecord> AdvertisementRecords { get; protected internal set; }

	#endregion Advertisments

	#region Rssi

	/// <summary>
	/// Last acquired <see cref="IRssi"/> value in decibels.
	/// </summary>
	protected internal IRssi Rssi { get; set; }

	public abstract Task<IRssi> GetRssi(CancellationToken cancellationToken = default);

	/*/// <summary>
	/// Updates the rssi value.
	/// </summary>
	public abstract Task<bool> UpdateRssiAsync(CancellationToken cancellationToken = default);*/

	#endregion Rssi

	#region Connection

	/// <summary>
	/// State of the device.
	/// </summary>
	public abstract DeviceState State { get; }

	/*/// <summary>
	/// Gets the <see cref="DeviceBondState"/> of the device.
	/// </summary>
	public abstract DeviceBondState BondState { get; }*/

	/*/// <summary>
	/// Gets the <see cref="DeviceBondState"/> of the device.
	/// </summary>
	protected abstract DeviceBondState GetBondState();*/

	/// <summary>
	/// Shows whether the device supports the <see cref="IsConnectable"/>.
	/// </summary>
	public virtual bool SupportsIsConnectable => true;

	/// <summary>
	/// Reflects if the device is connectable.
	/// Only supported if <see cref="SupportsIsConnectable"/> is true.
	/// </summary>
	public virtual bool IsConnectable { get; }

	///// <summary>
	///// Determines the state of the device.
	///// </summary>
	//protected abstract DeviceState GetState();

	/// <summary>
	/// Updates the connection parameters if already connected
	/// </summary>
	/// <param name="connectParameters"></param>
	/// <returns></returns>
	public abstract bool UpdateConnectionParameters(ConnectParameters connectParameters = default);

	/// <summary>
	/// Requests a bluetooth-le connection update request.
	/// </summary>
	public bool UpdateConnectionInterval(ConnectionInterval interval) => UpdateConnectionIntervalNative(interval);

	/// <summary>
	/// Native implementation of <c>UpdateConnectionInterval</c>.
	/// </summary>
	protected abstract bool UpdateConnectionIntervalNative(ConnectionInterval interval);

	#endregion Connection

	#region Mtu

	/// <summary>
	/// Requests a MTU update and fires an "Exchange MTU Request" on the ble stack.
	/// </summary>
	public async Task<int> RequestMtuAsync(int requestValue, CancellationToken cancellationToken = default)
	{
		return await RequestMtuNativeAsync(requestValue, cancellationToken);
	}

	/// <summary>
	/// Native implementation of <c>RequestMtuAsync</c>.
	/// </summary>
	protected abstract Task<int> RequestMtuNativeAsync(int requestValue, CancellationToken cancellationToken);

	#endregion Mtu

	#region Services

	/// <summary>
	/// Gets all services of the device.
	/// </summary>
	public async Task<IReadOnlyList<IService>> GetServicesAsync(CancellationToken cancellationToken = default)
	{
		lock (_knownServices)
		{
			if (_knownServices.Any())
				return _knownServices.ToArray();
		}

		using (var source = this.GetCombinedSource(cancellationToken))
		{
			var services = await GetServicesNativeAsync(cancellationToken);

			lock (_knownServices)
			{
				if (services != null)
					_knownServices.AddRange(services);

				return _knownServices.ToArray();
			}
		}
	}

	/// <summary>
	/// Gets the first service with the ID <paramref name="id"/>.
	/// </summary>
	/// <param name="id">The id of the searched service.</param>
	/// <param name="cancellationToken"></param>
	public async Task<IService> GetServiceAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var services = await GetServicesAsync(cancellationToken);
		return services.ToList().FirstOrDefault(x => x.Id == id);
	}


	/// <summary>
	/// Native implementation of <c>GetServicesAsync</c>.
	/// </summary>
	protected abstract Task<IReadOnlyList<IService>> GetServicesNativeAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Currently not being used anywhere!
	/// </summary>
	protected abstract Task<IService> GetServiceNativeAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Clear all (known) services.
	/// </summary>
	public void ClearServices()
	{
		this.CancelEverythingAndReInitialize();

		lock (_knownServices)
		{
			foreach (var service in _knownServices)
			{
				try
				{
					service.Dispose();
				}
				catch (Exception ex)
				{
					Trace.Message("Exception while cleanup of service: {0}", ex.Message);
				}
			}

			_knownServices.Clear();
		}
	}

	#endregion Services

	#region Eliminate

	object IDevice.NativeDevice => NativeDevice;

	/// <summary>
	/// The native device.
	/// </summary>
	public TNativeDevice NativeDevice { get; protected set; }

	#endregion Eliminate

	#region Overrides

	/// <summary>
	/// Convert to string (using the advertised device name).
	/// </summary>
	public override string ToString() => Name;

	/// <summary>
	/// Equality operator for comparison with other devices.
	/// Checks for equality of the <c>Id</c>.
	/// </summary>
	public override bool Equals(object other)
	{
		if (other == null)
			return false;

		if (other.GetType() != GetType())
			return false;

		var otherDeviceBase = (DeviceBase<TNativeDevice>)other;
		return Id == otherDeviceBase.Id;
	}

	/// <summary>
	/// Returns the hash code for this instance
	/// (using the hash code of the <c>Id</c>).
	/// </summary>
	public override int GetHashCode() => Id.GetHashCode();

	#endregion Overrides

	/// <summary>
	/// Dispose the device.
	/// </summary>
	public virtual void Dispose() => _adapter.DisconnectDeviceAsync(this);
}