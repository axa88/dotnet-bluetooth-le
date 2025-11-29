using System;
using System.Threading;
using System.Threading.Tasks;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Shared.Contracts.RequestResults;


namespace Plugin.BLE.Shared.Contracts.Connection.Adapter;

public interface IConnectRequest
{
	Task<IResult> ConnectToDevice(IDevice device, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default);

	Task<IResult> ConnectToDeviceById(Guid id, ConnectParameters connectParameters = default, CancellationToken cancellationToken = default);

	Task<IResult> DisconnectDevice(IDevice device, CancellationToken cancellationToken = default);
}
