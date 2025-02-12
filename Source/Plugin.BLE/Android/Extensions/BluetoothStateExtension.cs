using Android.Bluetooth;
using Plugin.BLE.Abstractions.Contracts;

namespace Plugin.BLE.Extensions;

public static class BluetoothStateExtension
{
	public static BluetoothState ToBluetoothState(this State state)
	{
		return state switch
		{
			State.Connected or State.Connecting or State.Disconnected or State.Disconnecting => BluetoothState.On,
			State.Off => BluetoothState.Off,
			State.On => BluetoothState.On,
			State.TurningOff => BluetoothState.TurningOff,
			State.TurningOn => BluetoothState.TurningOn,
			_ => BluetoothState.Unknown
		};
	}
}