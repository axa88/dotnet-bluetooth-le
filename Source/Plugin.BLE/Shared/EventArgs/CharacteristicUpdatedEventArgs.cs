using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE.Abstractions.EventArgs
{
	/// <summary>
	/// Event arguments for <see cref="ICharacteristic.ValueUpdated"/>
	/// </summary>
	public class CharacteristicUpdatedEventArgs(ICharacteristic characteristic) : System.EventArgs
	{
		/// <summary>
		/// The characteristic.
		/// </summary>
		public ICharacteristic Characteristic => characteristic;
	}
}