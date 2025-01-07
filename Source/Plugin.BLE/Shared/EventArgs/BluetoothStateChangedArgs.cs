using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE.Abstractions.EventArgs
{
	/// <summary>
	/// Event arguments for <see cref="BleImplementationBase.StateChanged"/>
	/// </summary>
	public class BluetoothStateChangedArgs(BluetoothState oldState, BluetoothState newState) : System.EventArgs
	{
		/// <summary>
		/// State before the change.
		/// </summary>
		public BluetoothState OldState => oldState;

		/// <summary>
		/// Current state.
		/// </summary>
		public BluetoothState NewState => newState;
	}
}