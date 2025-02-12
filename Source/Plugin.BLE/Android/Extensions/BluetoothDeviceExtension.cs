using Android.Bluetooth;

namespace Plugin.BLE.Android.Extensions;

public static class BluetoothDeviceExtension
{
	public static bool SupportsBle(this BluetoothDevice d) => d.Type is BluetoothDeviceType.Le or BluetoothDeviceType.Dual;
}