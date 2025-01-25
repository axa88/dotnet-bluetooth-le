using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Shared.Contracts.RequestResults;


namespace Plugin.BLE.Shared.Contracts.Connection;

/// <summary>
///  The final results of the Connection Request
/// </summary>
/// <param name="resultStatus"> Result status. This depends on platforms cooperation in reporting.
/// Either <see cref="ResultStatus.Success"/> or a failure <see cref="ResultStatus.SpecifiedFailure"/> with, or <see cref="ResultStatus.UnspecifiedFailure"/> without, specification. </param>
/// <param name="detail"> String representation of the <see cref="ResultStatus" /> </param>
public class ConnectionResult(ResultStatus resultStatus, string detail = "", IDevice device = null) : IResult
{
	public ResultStatus ResultStatus { get; } = resultStatus;
	public string Detail { get; } = detail;
	public IDevice Device { get; } = device;
}
