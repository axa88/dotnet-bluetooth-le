namespace Plugin.BLE.Shared.Contracts.Pairing;

/// <summary>
/// Suggested pairing options when requesting a Bond
/// </summary>
/// <param name="requestedModes"> Any or all modes acceptable for the application </param>
/// <param name="minimumRequestedProtection"> The minimal protection level (Authentication and or Encryption) that satisfies the application's communication requirements </param>
public class BondingOptions(PairModes requestedModes = PairModes.None, ProtectionLevel minimumRequestedProtection = ProtectionLevel.Unused)
{
	public PairModes RequestedModes { get; } = requestedModes;
	public ProtectionLevel MinimumRequestedProtection { get; } = minimumRequestedProtection;
}
