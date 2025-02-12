using System;
using System.Collections.Generic;

using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;


namespace Plugin.BLE.Shared.Contracts.Pairing.Adapter;

/// <summary>
/// Indicate the platform's <see cref="IAdapter"/> is able to support monitor Bonded Devices
/// </summary>
public interface IBondReport
{
	/// <summary>
	/// Occurs when the bonding state of a device changed
	/// </summary>
	public event EventHandler<DeviceBondStateChangedEventArgs> DeviceBondStateChanged;

	/// <summary>
	/// List of currently bonded devices.
	/// </summary>
	public IReadOnlyList<IDevice> BondedDevices { get; }
}
