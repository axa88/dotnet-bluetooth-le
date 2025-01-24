using System;

using Android.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Shared.Contracts.Pairing;


namespace Plugin.BLE.Extensions;

internal static class DeviceBondStateExtension
{
	public static DeviceBondState XPlatformBondState(this Bond bondState)
	{
		return bondState switch
		{
			Bond.None => DeviceBondState.NotBonded,
			Bond.Bonding => DeviceBondState.Bonding,
			Bond.Bonded => DeviceBondState.Bonded,
			_ => DeviceBondState.NotSupported
		};
	}

	internal static BondStatus XPlatformBondStatus(this Bond pairStatus)
	{
		return pairStatus switch
		{
			Bond.Bonded => BondStatus.Paired,
			Bond.Bonding => BondStatus.SpecifiedFailure,
			Bond.None => BondStatus.UnspecifiedFailure,
			_ => throw new ArgumentOutOfRangeException(nameof(pairStatus), pairStatus, null)
		};
	}
}
