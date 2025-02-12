using System;
using Android.Bluetooth;
using Android.Content;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Extensions;

namespace Plugin.BLE.BroadcastReceivers;

public class BluetoothStatusBroadcastReceiver(Action<BluetoothState> stateChangedHandler) : BroadcastReceiver
{
	public override void OnReceive(Context context, Intent intent)
	{
		var action = intent.Action;

		if (action != BluetoothAdapter.ActionStateChanged)
			return;

		var state = intent.GetIntExtra(BluetoothAdapter.ExtraState, -1);

		if (state == -1)
		{
			stateChangedHandler?.Invoke(BluetoothState.Unknown);
			return;
		}

		var btState = (State)state;
		stateChangedHandler?.Invoke(btState.ToBluetoothState());
	}
}