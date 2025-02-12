namespace Plugin.BLE.Shared.Contracts.RequestResults;

/// <summary>
/// Represents a Command Result, all commands will implement this
/// </summary>
public interface IResult
{
	public ResultStatus ResultStatus { get; }
	public string Detail { get; }
}
