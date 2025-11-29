using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Plugin.BLE.Shared.Contracts.Rssi;


namespace Plugin.BLE.Abstractions.Contracts;

/// <summary>
/// A bluetooth LE device.
/// </summary>
public interface IDevice : IDisposable
{
	/// <summary>
	/// ID of the device.
	/// </summary>
	Guid Id { get; }

	/// <summary>
	/// Advertised Name of the Device.
	/// </summary>
	string Name { get; }

	Task<IRssi> GetRssi(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the native device object reference. Should be cast to the appropriate type on each platform.
	/// </summary>
	/// <value>The native device.</value>
	object NativeDevice { get; }

	/// <summary>
	/// Gets the information if the device has hinted during advertising that the device is connectable.
	/// This information is not part of an advertising record. It's determined from the PDU header.
	/// Check <see cref="SupportsIsConnectable"/> to verify that the device supports IsConnectable.
	/// If the device doesn't support IsConnectable then IsConnectable returns true.
	/// </summary>
	bool IsConnectable { get; }

	/// <summary>
	/// True if device supports IsConnectable
	/// </summary>
	bool SupportsIsConnectable { get; }

	/// <summary>
	/// State of the device.
	/// ToDo Redefine. What does a connection mean?
	/// On windows the connection means a Gatt connection was established by the OS
	/// so if other instances of a Device are made, whether in the same or another application,
	/// figure out if it can be used by any other device/application.
	/// if so the limited state should not be used
	/// 
	/// On Android each Device instance can create its own Gatt, therefore connection is at an individual Device level.
	/// Therefore, a connection means if a particular Device has communication on its own Gatt.
	/// Here the Limited state has no meaning, it should simply be disconnected
	/// 
	/// if the above proves true, then eliminate the contrived <cref>DeviceState.Limited</cref> state
	/// </summary>
	DeviceState State { get; }

	/// <summary>
	/// Requests a bluetooth-le connection update request. Be aware that this is only implemented on Android (>= API 21). 
	/// You can choose between a high, low and a normal mode which will requests the following connection intervals: HIGH (11-15ms). NORMAL (30-50ms). LOW (100-125ms).
	/// It's not possible to request a specific connection interval.
	/// </summary>
	/// <remarks>
	/// Important:
	/// On Android: Starting from API level 34, this always requests an MTU of 517 (and the requested value is ignored).
	/// On iOS: Updating the connection interval is not supported by iOS. The function simply returns false.
	/// </remarks>
	/// <returns>True if the update request was successful. On iOS, it will always return false.</returns>
	/// <param name="interval">The requested interval (High/Low/Normal)</param>
	bool UpdateConnectionInterval(ConnectionInterval interval);

	/// <summary>
	/// Updates the connection parameters if already connected
	/// </summary>
	/// <remarks>
	/// Only implemented for Windows
	/// </remarks>
	/// <param name="connectParameters">Connection parameters. Contains platform specific parameters needed to achieve connection. The default value is None.</param>
	/// <returns>
	/// The Result property will contain a boolean that indicates if the update was successful.        
	/// </returns>
	bool UpdateConnectionParameters(ConnectParameters connectParameters = default);

	/// <summary>
	/// All the advertisement records
	/// For example:
	/// - Advertised Service UUIDS
	/// - Manufacturer Specific data
	/// - ...
	/// </summary>
	IReadOnlyList<AdvertisementRecord> AdvertisementRecords { get; }

	/// <summary>
	/// Gets all services of the device.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns>A task that represents the asynchronous read operation. The Result property will contain a list of all available services.</returns>
	Task<IReadOnlyList<IService>> GetServicesAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the first service with the ID <paramref name="id"/>. 
	/// </summary>
	/// <param name="id">The id of the searched service.</param>
	/// <param name="cancellationToken"></param>
	/// <returns>
	/// A task that represents the asynchronous read operation. 
	/// The Result property will contain the service with the specified <paramref name="id"/>.
	/// If the service doesn't exist, the Result will be null.
	/// </returns>
	Task<IService> GetServiceAsync(Guid id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Requests an MTU update and fires an "Exchange MTU Request" on the ble stack.
	/// Be aware that the resulting MTU value will be negotiated between master and slave using your requested value for the negotiation.
	/// </summary>
	/// <remarks>
	/// Important: 
	/// On Android: This function will only work with API level 21 and higher. Other API level will get a default value as function result.
	/// On iOS: Requesting MTU sizes is not supported by iOS. The function will return the current negotiated MTU between master / slave.
	/// On Windows: Requesting MTU sizes is not directly supported. Windows will always try and negotiate the maximum MTU between master / slave. The function will return the current negotiated MTU between master / slave.
	/// </remarks>
	/// <returns>
	/// A task that represents the asynchronous operation. The result contains the negotiated MTU size between master and slave</returns>
	/// <param name="requestValue">The requested MTU</param>
	/// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is None.</param>
	Task<int> RequestMtuAsync(int requestValue, CancellationToken cancellationToken = default);
}