using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE.Shared.Contracts.Pairing.Device;

/// <summary>
/// Indicate a <see cref="IDevice"/> is able to report its current platform Bond state
/// </summary>
public interface IBondState
{
	/// <summary>
	/// Gets the bond state of a device.
	/// </summary>
	DeviceBondState BondState { get; }
}
