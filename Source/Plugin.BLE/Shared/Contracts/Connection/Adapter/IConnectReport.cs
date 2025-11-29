using System;
using System.Collections.Generic;

using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;


namespace Plugin.BLE.Shared.Contracts.Connection.Adapter;

public interface IConnectReport
{
	public event EventHandler<DeviceConnectionChangedEventArgs> DeviceConnectionStateChanged;

	IReadOnlyList<IDevice> ConnectedDevices { get; }
}
