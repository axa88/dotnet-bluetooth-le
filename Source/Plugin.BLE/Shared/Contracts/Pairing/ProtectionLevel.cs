namespace Plugin.BLE.Shared.Contracts.Pairing;

public enum ProtectionLevel
{
	Unused,

	/// <summary>
	/// Pair the device using no level of protection.
	/// see: "Windows.Devices.Enumeration.DevicePairingProtectionLevel.None"
	/// </summary>
	None,

	/// <summary>
	/// Pair the device using encryption.
	/// see: "Windows.Devices.Enumeration.DevicePairingProtectionLevel.Encryption"
	/// </summary>
	Encryption,

	/// <summary>
	/// Pair the device using encryption and authentication.
	/// see: "Windows.Devices.Enumeration.DevicePairingProtectionLevel.EncryptionAndAuthentication"
	/// </summary>
	EncryptionAndAuthentication
}
