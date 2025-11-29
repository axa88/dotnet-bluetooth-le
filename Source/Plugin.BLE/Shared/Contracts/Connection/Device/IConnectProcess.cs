using System.Threading;
using System.Threading.Tasks;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Shared.Contracts.RequestResults;


namespace Plugin.BLE.Shared.Contracts.Connection.Device;

internal interface IConnectProcess
{
	internal Task<IResult> ConnectInternalX(ConnectParameters connectParameters, CancellationToken cancellationToken);

}
