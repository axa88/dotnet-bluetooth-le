using System;

using Android.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Shared.Contracts.RequestResults;


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

	internal static ResultStatus XPlatformBondStatus(this Bond pairStatus)
	{
		return pairStatus switch
		{
			Bond.Bonded => ResultStatus.Success,
			Bond.Bonding => ResultStatus.SpecifiedFailure,
			Bond.None => ResultStatus.UnspecifiedFailure,
			_ => throw new ArgumentOutOfRangeException(nameof(pairStatus), pairStatus, null)
		};
	}
}
