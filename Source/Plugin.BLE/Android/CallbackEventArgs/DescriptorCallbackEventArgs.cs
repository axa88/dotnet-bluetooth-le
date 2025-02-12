using System;
using Android.Bluetooth;
using Plugin.BLE.Abstractions.Exceptions;
namespace Plugin.BLE.Android.CallbackEventArgs
{
	public class DescriptorCallbackEventArgs(BluetoothGattDescriptor descriptor, Exception exception = null)
	{
		public BluetoothGattDescriptor Descriptor { get; } = descriptor;
		public Exception Exception { get; } = exception;
	}
}
