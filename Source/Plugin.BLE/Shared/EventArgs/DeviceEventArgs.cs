using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE.Abstractions.EventArgs
{
	/// <summary>
	/// Event arguments for device events in <see cref="IAdapter"/>,
	/// see also: <seealso cref="AdapterBase.DeviceAdvertised"/>,
	/// <seealso cref="AdapterBase.DeviceDiscovered"/>,
	/// <seealso cref="AdapterBase.DeviceConnected"/>,
	/// <seealso cref="AdapterBase.DeviceDisconnected"/>
	/// </summary>
	public class DeviceEventArgs(IDevice device) : System.EventArgs
	{
		/// <summary>
		/// The device.
		/// </summary>
		public IDevice Device => device;
	}
}