using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE.Abstractions.EventArgs;

public class DeviceBondStateChangedEventArgs(IDevice device, string address, DeviceBondState state) : DeviceEventArgs(device)
{
	/// <summary>
	/// The device address.
	/// </summary>
	public string Address => address;

	/// <summary>
	/// The bond state.
	/// </summary>
	public DeviceBondState State => state;
}