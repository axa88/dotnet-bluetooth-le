namespace Plugin.BLE.Shared.Contracts.RequestResults;

public enum ResultStatus
{
	/// <summary>
	/// Result is simply that the device is known to be in the requested state upon task completion.
	/// </summary>
	Success,

	/// <summary>
	/// The device is known not to be in the requested state upon task completion. But there is ambiguity if the operation failed or was canceled/timed out, or otherwise; and the reasoning is not specified.
	/// </summary>
	UnspecifiedFailure,

	/// <summary>
	/// The device is known not to be in the requested state upon task completion. There is no ambiguity the operation failed, it wasn't canceled/timed out, or otherwise; and the reasoning is given.
	/// </summary>
	SpecifiedFailure
}
