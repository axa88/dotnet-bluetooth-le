using System;


namespace Plugin.BLE.Shared.Contracts.Rssi;

public class RssiBase : IRssi
{
	#region Implementation of IRssi

	public virtual DateTime Timestamp { get; set; } = DateTime.MaxValue; // value invalid until set
	public sbyte Value { get; set; } = 0; // reasonable default value as largely all valid values are negative. standards are hard to find on this

	#endregion

	#region Overrides of Object

	public override string ToString() => $"{Timestamp:HH:mm:ss} {Value} dBm";

	public override bool Equals(object obj)
	{
		if (obj == null || GetType() != obj.GetType())
			return false;

		var other = (RssiBase)obj;
		return Timestamp == other.Timestamp && Value == other.Value;
	}

	public override int GetHashCode()
	{
		#if UAP10_0_19041 || NETSTANDARD
		unchecked
		{
			var hash = 17;
			hash = hash * 23 + Timestamp.GetHashCode();
			hash = hash * 23 + Value.GetHashCode();
			return hash;
		}
		#else
		return HashCode.Combine(Timestamp, Value);
		#endif
	}

	#endregion
}
