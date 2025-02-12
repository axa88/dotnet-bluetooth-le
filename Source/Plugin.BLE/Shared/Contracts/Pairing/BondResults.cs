using Plugin.BLE.Shared.Contracts.RequestResults;


namespace Plugin.BLE.Shared.Contracts.Pairing;


/// <summary>
///  The final results of the Bond Request
/// </summary>
/// <param name="status"> Result status. This depends on platforms cooperation in reporting.
/// In addition to success, Windows and Android to a lesser extent may report information on failure </param>
/// <param name="detail"> String representation of the <see cref="ResultStatus" /> </param>
public class BondResult(ResultStatus status, string detail = "") : IResult
{
	public ResultStatus ResultStatus { get; } = status;
	public string Detail { get; } = detail;
}

/// <summary>
/// The final results of the Bond Request made on Windows
/// </summary>
/// <param name="status"> Result status. This depends on platforms cooperation in reporting.
/// In addition to success, Windows and Android to a lesser extent may report information on failure </param>
/// <param name="detail"> String representation of the <see cref="BondResult.ResultStatus" /> </param>
/// <param name="protectionUsed"> The negotiated protection level (Authentication and or Encryption) used for communication with the remote device </param>
public class BondResultManualPair(ResultStatus status, string detail, ProtectionLevel protectionUsed) : BondResult(status, detail)
{
	public ProtectionLevel ProtectionUsed { get; } = protectionUsed;
}