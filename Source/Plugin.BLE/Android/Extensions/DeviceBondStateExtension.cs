using System;

using Android.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts.Bonding;


namespace Plugin.BLE.Extensions;

internal static class DeviceBondStateExtension
{
	public static DeviceBondState XPlatformBondState(this Bond bondState)
	{
		switch (bondState)
		{
			case Bond.None:
				return DeviceBondState.NotBonded;
			case Bond.Bonding:
				return DeviceBondState.Bonding;
			case Bond.Bonded:
				return DeviceBondState.Bonded;
			default:
				return DeviceBondState.NotSupported;
		}
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
