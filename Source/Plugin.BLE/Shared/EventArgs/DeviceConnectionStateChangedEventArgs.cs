using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE.Abstractions.EventArgs;

public class DeviceConnectionChangedEventArgs(IDevice device, bool connectionLost = false) : DeviceEventArgs(device)
{
	public bool ConnectionLost { get; } = connectionLost;
}
