using System;
using Plugin.BLE.Abstractions.Contracts;

namespace Plugin.BLE.Android.CallbackEventArgs;

public class MtuRequestCallbackEventArgs(Exception error, int mtu) : EventArgs
{
	public Exception Error { get; } = error;
	public int Mtu { get; } = mtu;
}