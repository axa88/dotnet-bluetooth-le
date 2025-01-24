using System;


namespace Plugin.BLE.Shared.Contracts;

public interface ITimestamp
{
	DateTime Timestamp { get; internal set; }
}