using System;

using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE
{
	/// <summary>
	/// Cross-platform bluetooth LE implementation.
	/// </summary>
	public static class CrossBluetoothLE
	{
		private static readonly Lazy<IBluetoothLE> Implementation = new(CreateImplementation, System.Threading.LazyThreadSafetyMode.PublicationOnly);

		/// <summary>
		/// Current bluetooth LE implementation.
		/// </summary>
		public static IBluetoothLE Current => Implementation.Value ?? throw NotImplementedInReferenceAssembly();

		private static IBluetoothLE CreateImplementation()
		{
			var implementation = new BleImplementation();
			implementation.Initialize();
			return implementation;
		}

		private static Exception NotImplementedInReferenceAssembly() => new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
	}
}