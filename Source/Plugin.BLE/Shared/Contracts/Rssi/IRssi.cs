namespace Plugin.BLE.Shared.Contracts.Rssi;

/// <summary>
/// Initialize Timestamp with DateTime.MaxValue if platform not supported
/// </summary>
public interface IRssi : ITimestamp
{
	sbyte Value { get; internal set; }
}