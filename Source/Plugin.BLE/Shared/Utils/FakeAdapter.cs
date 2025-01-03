using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Plugin.BLE.Abstractions.Contracts;

namespace Plugin.BLE.Abstractions.Utils;

internal class FakeAdapter : AdapterBase
{
	protected override Task<IDevice> ConnectToKnownDeviceNativeAsync(Guid deviceGuid, ConnectParameters connectParameters, CancellationToken cancellationToken)
	{
		TraceUnavailability();
		return Task.FromResult<IDevice>(null);
	}

	protected override Task StartScanningForDevicesNativeAsync(ScanFilterOptions scanFilterOptions, bool allowDuplicatesKey, CancellationToken scanCancellationToken)
	{
		TraceUnavailability();
		return Task.FromResult(0);
	}

	protected override void StopScanNative()
	{
		TraceUnavailability();
	}

	protected override Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters, CancellationToken cancellationToken)
	{
		TraceUnavailability();
		return Task.FromResult(0);
	}

	protected override void DisconnectDeviceNative(IDevice device)
	{
		TraceUnavailability();
	}

	private static void TraceUnavailability()
	{
		Trace.Message("Bluetooth LE is not available on this device. Nothing will happen - ever!");
	}

	public override IReadOnlyList<IDevice> GetConnectedOrBondedDevices(Guid[] services = null)
	{
		TraceUnavailability();
		return new List<IDevice>();
	}

	public override IReadOnlyList<IDevice> GetConnectedOrBondedDevicesByIds(Guid[] ids)
	{
		TraceUnavailability();
		return new List<IDevice>();
	}
}