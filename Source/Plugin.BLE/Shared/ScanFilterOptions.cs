using System;
using System.Linq;

namespace Plugin.BLE.Abstractions;

/// <summary>
/// A scan filter for service data (including UUID and actual data).
/// Android only.
/// </summary>
public class ServiceDataFilter(Guid guid, byte[] data = null, byte[] mask = null)
{
	/// <summary>
	/// Constructor with UUID as string.
	/// </summary>
	public ServiceDataFilter(string uuid, byte[] data = null, byte[] mask = null) : this(new Guid(uuid), data, mask) { }

	/// <summary>
	/// The service-data UUID.
	/// </summary>
	public Guid ServiceDataUuid { get; set; } = guid;

	/// <summary>
	/// The service data (as a byte array).
	/// </summary>
	public byte[] ServiceData { get; set; } = data ?? [];

	/// <summary>
	/// The service-data mask (as a byte array).
	/// </summary>
	public byte[] ServiceDataMask { get; set; } = mask;
}

/// <summary>
/// A scan filter for manufacturer data (including manufacturer ID and actual data).
/// Android only.
/// </summary>
public class ManufacturerDataFilter(ushort mId, byte[] data = null, byte[] mask = null)
{
	/// <summary>
	/// The manufacturer ID.
	/// </summary>
	public ushort ManufacturerId { get; set; } = mId;

	/// <summary>
	/// The manufacturer data (as a byte array).
	/// </summary>
	public byte[] ManufacturerData { get; set; } = data ?? [];

	/// <summary>
	/// The manufacturer-data mask (as a byte array).
	/// </summary>
	public byte[] ManufacturerDataMask { get; set; } = mask;
}

/// <summary>
/// Pass one or multiple scan filters to filter the scan. Pay attention to which filters are platform specific.
/// At least one scan filter is required to enable scanning whilst the screen is off in Android.
/// </summary>
public class ScanFilterOptions
{
	/// <summary>
	/// Android/iOS/MacOS. Filter the scan by advertised service ID(s).
	/// </summary>
	public Guid[] ServiceUuids { get; set; }

	/// <summary>
	/// Android only. Filter the scan by service data.
	/// </summary>
	public ServiceDataFilter[] ServiceDataFilters { get; set; } = null;

	/// <summary>
	/// Android only. Filter the scan by device address(es)
	/// </summary>
	public string[] DeviceAddresses { get; set; } = null;

	/// <summary>
	/// Android only. Filter the scan by manufacturer data.
	/// </summary>
	public ManufacturerDataFilter[] ManufacturerDataFilters { get; set; } = null;

	/// <summary>
	/// Android only. Filter the scan by device name(s).
	/// </summary>
	public string[] DeviceNames { get; set; } = null;

	/// <summary>
	/// Indicates whether the options include any filter at all.
	/// </summary>
	public bool HasFilter => HasServiceIds || HasServiceData || HasDeviceAddresses || HasManufacturerIds || HasDeviceNames;

	/// <summary>
	/// Indicates whether the options include a filter on service Ids.
	/// </summary>

	public bool HasServiceIds => ServiceUuids?.Any() == true;
	/// <summary>
	/// Indicates whether the options include a filter on service data.
	/// </summary>

	public bool HasServiceData => ServiceDataFilters?.Any() == true;
	/// <summary>
	/// Indicates whether the options include a filter on device addresses.
	/// </summary>

	public bool HasDeviceAddresses => DeviceAddresses?.Any() == true;
	/// <summary>
	/// Indicates whether the options include a filter on manufacturer data.
	/// </summary>
	public bool HasManufacturerIds => ManufacturerDataFilters?.Any() == true;

	/// <summary>
	/// Indicates whether the options include a filter on device names.
	/// </summary>
	public bool HasDeviceNames => DeviceNames?.Any() == true;
}