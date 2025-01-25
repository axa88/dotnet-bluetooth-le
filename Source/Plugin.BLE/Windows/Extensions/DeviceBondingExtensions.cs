using System;

using Windows.Devices.Enumeration;

using Plugin.BLE.Shared.Contracts.Pairing;
using Plugin.BLE.Shared.Contracts.RequestResults;


namespace Plugin.BLE.Extensions;

internal static class DeviceBondingExtensions
{
	public static ResultStatus XPlatformPairStatus(this DevicePairingResultStatus pairStatus)
	{
		return pairStatus switch
		{
			DevicePairingResultStatus.Paired => ResultStatus.Success,
			DevicePairingResultStatus.NotPaired or DevicePairingResultStatus.Failed => ResultStatus.UnspecifiedFailure,
			DevicePairingResultStatus.NotReadyToPair
				or DevicePairingResultStatus.AlreadyPaired
				or DevicePairingResultStatus.ConnectionRejected
				or DevicePairingResultStatus.TooManyConnections
				or DevicePairingResultStatus.HardwareFailure
				or DevicePairingResultStatus.AuthenticationTimeout
				or DevicePairingResultStatus.AuthenticationNotAllowed
				or DevicePairingResultStatus.AuthenticationFailure
				or DevicePairingResultStatus.NoSupportedProfiles
				or DevicePairingResultStatus.ProtectionLevelCouldNotBeMet
				or DevicePairingResultStatus.AccessDenied
				or DevicePairingResultStatus.InvalidCeremonyData
				or DevicePairingResultStatus.PairingCanceled
				or DevicePairingResultStatus.OperationAlreadyInProgress
				or DevicePairingResultStatus.RequiredHandlerNotRegistered
				or DevicePairingResultStatus.RejectedByHandler
				or DevicePairingResultStatus.RemoteDeviceHasAssociation => ResultStatus.SpecifiedFailure,
			_ => throw new ArgumentOutOfRangeException(nameof(pairStatus), pairStatus, null)
		};
	}

	public static ProtectionLevel XPlatformProtectionLevel(this DevicePairingProtectionLevel protectionLevel)
	{
		return protectionLevel switch
		{
			DevicePairingProtectionLevel.Default => ProtectionLevel.Unused,
			DevicePairingProtectionLevel.None => ProtectionLevel.None,
			DevicePairingProtectionLevel.Encryption => ProtectionLevel.Encryption,
			DevicePairingProtectionLevel.EncryptionAndAuthentication => ProtectionLevel.EncryptionAndAuthentication,
			_ => throw new ArgumentOutOfRangeException(nameof(protectionLevel), protectionLevel, null)
		};
	}
}
