using Android.Bluetooth.LE;
using Plugin.BLE.Abstractions.Contracts;
using System;

namespace Plugin.BLE.Extensions;

internal static class ScanMatchModeExtension
{
	public static BluetoothScanMatchMode ToNative(this ScanMatchMode matchMode)
	{
		return matchMode switch
		{
			ScanMatchMode.AGRESSIVE => BluetoothScanMatchMode.Aggressive,
			ScanMatchMode.STICKY => BluetoothScanMatchMode.Sticky,
			_ => throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, null)
		};
	}
}