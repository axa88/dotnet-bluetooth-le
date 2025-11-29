using Windows.Devices.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Windows;
using System;
using System.Threading.Tasks;
using Windows.Devices.Radios;


namespace Plugin.BLE
{
	public class BleImplementation : BleImplementationBase
	{
		private BluetoothAdapter _btAdapter;
		private Radio _radio;
		private bool _isInitialized;

		public static BluetoothCacheMode CacheModeCharacteristicRead { get; set; } = BluetoothCacheMode.Uncached;
		public static BluetoothCacheMode CacheModeDescriptorRead { get; set; } = BluetoothCacheMode.Uncached;
		public static BluetoothCacheMode CacheModeGetDescriptors { get; set; } = BluetoothCacheMode.Cached;
		public static BluetoothCacheMode CacheModeGetCharacteristics { get; set; } = BluetoothCacheMode.Cached;
		public static BluetoothCacheMode CacheModeGetServices { get; set; } = BluetoothCacheMode.Cached;

		protected override IAdapter CreateNativeAdapter() => new Adapter(_btAdapter);

		protected override BluetoothState GetInitialStateNative()
			=> !_isInitialized ? BluetoothState.Unknown : !_btAdapter.IsLowEnergySupported ? BluetoothState.Unavailable : ToBluetoothState(_radio.State);

		private static BluetoothState ToBluetoothState(RadioState radioState)
			=> radioState switch
			{
				RadioState.On => BluetoothState.On,
				RadioState.Off => BluetoothState.Off,
				_ => BluetoothState.Unavailable
			};

		private void RadioStateChanged(Radio radio, object args) => State = ToBluetoothState(radio.State);

		protected override void InitializeNative()
		{
			try
			{
				_btAdapter = BluetoothAdapter.GetDefaultAsync().AsTask().Result;
				_radio = _btAdapter.GetRadioAsync().AsTask().Result;
				_radio.StateChanged += RadioStateChanged;
				_isInitialized = true;
			}
			catch (Exception ex) { Trace.Message("InitializeNative exception:{0}", ex.Message); }
		}

		public override async Task<bool> TrySetStateAsync(bool on)
		{
			if (!_isInitialized)
				return false;
			try
			{
				return await _radio.SetStateAsync(on ? RadioState.On : RadioState.Off) == RadioAccessStatus.Allowed;
			}
			catch (Exception ex)
			{
				Trace.Message("TrySetStateAsync exception: {0}", ex.Message);
				return false;
			}
		}
	}
}