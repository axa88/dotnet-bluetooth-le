using Android.Bluetooth;
using Plugin.BLE.Abstractions;

namespace Plugin.BLE.Extensions;

internal static class GattWriteTypeExtension
{
	public static CharacteristicWriteType ToCharacteristicWriteType(this GattWriteType writeType) => writeType.HasFlag(GattWriteType.NoResponse) ? CharacteristicWriteType.WithoutResponse : CharacteristicWriteType.WithResponse;
}