using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE.Abstractions.EventArgs
{
	/// <summary>
	/// Event arguments for device-error events in <see cref="IAdapter"/>.
	/// see also: <seealso cref="AdapterBase.DeviceConnectionLost"/>,
	/// <seealso cref="AdapterBase.DeviceConnectionError"/>
	/// </summary>
	public class DeviceErrorEventArgs(IDevice device, string error) : DeviceEventArgs(device)
	{
		/// <summary>
		/// The error message.
		/// </summary>
		public string ErrorMessage => error;
	}
}