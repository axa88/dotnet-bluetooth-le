using System;
using System.Runtime.CompilerServices;

using Android.Bluetooth;
using Android.OS;

using Plugin.BLE.Abstractions.Extensions;
using Plugin.BLE.Android.CallbackEventArgs;


namespace Plugin.BLE.Android;

public interface IGattCallback
{
	event EventHandler<MtuRequestCallbackEventArgs> MtuRequested;
	event EventHandler<RssiReadCallbackEventArgs> RemoteRssiRead;
	event EventHandler<ServicesDiscoveredCallbackEventArgs> ServicesDiscovered;
	event EventHandler<CharacteristicReadCallbackEventArgs> CharacteristicValueRead;
	event EventHandler<CharacteristicReadCallbackEventArgs> CharacteristicValueUpdated;
	event EventHandler<CharacteristicWriteCallbackEventArgs> CharacteristicValueWritten;
	event EventHandler<DescriptorCallbackEventArgs> DescriptorValueWritten;
	event EventHandler<DescriptorCallbackEventArgs> DescriptorValueRead;
	event EventHandler ConnectionInterrupted;
}

public class GattCallback(Adapter adapter, Device device) : BluetoothGattCallback, IGattCallback
{
	public event EventHandler<MtuRequestCallbackEventArgs> MtuRequested;
	public event EventHandler<RssiReadCallbackEventArgs> RemoteRssiRead;
	public event EventHandler<ServicesDiscoveredCallbackEventArgs> ServicesDiscovered;
	public event EventHandler<CharacteristicReadCallbackEventArgs> CharacteristicValueRead;
	public event EventHandler<CharacteristicReadCallbackEventArgs> CharacteristicValueUpdated;
	public event EventHandler<CharacteristicWriteCallbackEventArgs> CharacteristicValueWritten;
	public event EventHandler<DescriptorCallbackEventArgs> DescriptorValueWritten;
	public event EventHandler<DescriptorCallbackEventArgs> DescriptorValueRead;
	public event EventHandler ConnectionInterrupted;

	public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
	{
		if (!ParametersVerified(gatt, status))
			return;
		base.OnConnectionStateChange(gatt, status, newState);

		switch (newState)
		{
			case ProfileState.Disconnected:

				// Close GATT if autoConnect is disabled, else we can accumulate zombie gatts.
				if (!device.ConnectParameters.AutoConnect)
					CloseGattInstances(gatt);

				// If status == 19, then connection was closed by the peripheral device (clean disconnect), consider this as a DeviceDisconnected
				if (device.IsOperationRequested || (int)status == 19)
				{
					Abstractions.Trace.Message("Disconnected by user");

					//Found so we can remove it
					device.IsOperationRequested = false;
					adapter.ConnectedDeviceRegistry.TryRemove(gatt.Device.Address, out _);

					if (status != GattStatus.Success && (int)status != 19)
					{
						// The above error event handles the case where the error happened during a Connect call, which will close out any waiting asyncs.
						// Android > 5.0 uses this switch branch when an error occurs during connect
						Abstractions.Trace.Message($"Error while connecting '{device.Name}'. Not raising disconnect event.");
						adapter.HandleConnectionFail(device, $"GattCallback error: {status}");
					}
					else
					{
						//we already handled device error so no need th raise disconnect event(happens when device not in range)
						adapter.HandleDisconnectedDevice(true, device);
					}
				}
				else
				{
					//connection must have been lost, because the callback was not triggered by calling disconnect
					Abstractions.Trace.Message($"Disconnected '{device.Name}' by lost connection");

					adapter.ConnectedDeviceRegistry.TryRemove(gatt.Device.Address, out _);
					adapter.HandleDisconnectedDevice(false, device);

				}
				// inform pending tasks
				ConnectionInterrupted?.Invoke(this, EventArgs.Empty);
				break;
			case ProfileState.Connecting:
				Abstractions.Trace.Message("Connecting");
				break;
			case ProfileState.Connected:
				Abstractions.Trace.Message("Connected");

				//Check if the operation was requested by the user
				if (device.IsOperationRequested)
				{
					device.Update(gatt.Device, gatt);

					//Found so we can remove it
					device.IsOperationRequested = false;
				}
				else
				{
					//ToDo explore this
					//only for on auto-reconnect (device is not in operation registry)
					device.Update(gatt.Device, gatt);
				}

				if (status != GattStatus.Success)
				{
					// The above error event handles the case where the error happened during a Connect call, which will close out any waiting asyncs.
					// Android <= 4.4 uses this switch branch when an error occurs during connect
					Abstractions.Trace.Message($"Error while connecting '{device.Name}'. GattStatus: {status}. ");
					adapter.HandleConnectionFail(device, $"GattCallback error: {status}");

					CloseGattInstances(gatt);
				}
				else
				{
					adapter.ConnectedDeviceRegistry[gatt.Device.Address] = device;
					adapter.HandleConnectedDevice(device);
				}

				break;
			case ProfileState.Disconnecting:
				Abstractions.Trace.Message("Disconnecting");
				break;
		}
	}

	public override void OnMtuChanged(BluetoothGatt gatt, int mtu, GattStatus status)
	{
		if (!ParametersVerified(gatt, status))
			return;

		Abstractions.Trace.Message($"{nameof(mtu)}: {mtu}");
		base.OnMtuChanged(gatt, mtu, status);

		MtuRequested?.Invoke(this, new(GetExceptionFromGattStatus(status), mtu));
	}

	public override void OnReadRemoteRssi(BluetoothGatt gatt, int rssi, GattStatus status)
	{
		if (!ParametersVerified(gatt, status))
			return;

		Abstractions.Trace.Message($"{nameof(rssi)}: {rssi}");
		base.OnReadRemoteRssi(gatt, rssi, status);

		RemoteRssiRead?.Invoke(this, new(GetExceptionFromGattStatus(status), rssi));
	}

	public override void OnServicesDiscovered(BluetoothGatt gatt, GattStatus status)
	{
		if (!ParametersVerified(gatt, status))
			return;

		base.OnServicesDiscovered(gatt, status);

		ServicesDiscovered?.Invoke(this, new());
	}

	public override void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
	{
		if (!ParametersVerified(gatt, status))
			return;

		Abstractions.Trace.Message($"raw value: {characteristic.GetValue().ToHexString()}");
		base.OnCharacteristicRead(gatt, characteristic, status);

		CharacteristicValueRead?.Invoke(this, new(characteristic, status));
	}

	public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
	{
		if (!ParametersVerified(gatt))
			return;

		Abstractions.Trace.Message($"raw value: {characteristic.GetValue().ToHexString()}");
		base.OnCharacteristicChanged(gatt, characteristic);

		CharacteristicValueUpdated?.Invoke(this, new(characteristic, GattStatus.Success));
	}

	public override void OnCharacteristicWrite(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
	{
		if (!ParametersVerified(gatt, status))
			return;

		Abstractions.Trace.Message($"raw value: {characteristic.GetValue().ToHexString()}");
		base.OnCharacteristicWrite(gatt, characteristic, status);

		CharacteristicValueWritten?.Invoke(this, new(characteristic, status, GetExceptionFromGattStatus(status)));
	}

	public override void OnReliableWriteCompleted(BluetoothGatt gatt, GattStatus status)
	{
		if (!ParametersVerified(gatt, status))
			return;

		base.OnReliableWriteCompleted(gatt, status);
	}

	public override void OnDescriptorWrite(BluetoothGatt gatt, BluetoothGattDescriptor descriptor, GattStatus status)
	{
		if (!ParametersVerified(gatt, status))
			return;

		Abstractions.Trace.Message($"raw value: {descriptor.GetValue()?.ToHexString()}");
		base.OnDescriptorWrite(gatt, descriptor, status);

		DescriptorValueWritten?.Invoke(this, new(descriptor, GetExceptionFromGattStatus(status)));
	}

	public override void OnDescriptorRead(BluetoothGatt gatt, BluetoothGattDescriptor descriptor, GattStatus status)
	{
		if (!ParametersVerified(gatt, status))
			return;

		Abstractions.Trace.Message($"raw value: {descriptor.GetValue()?.ToHexString()}");
		base.OnDescriptorRead(gatt, descriptor, status);

		DescriptorValueRead?.Invoke(this, new(descriptor, GetExceptionFromGattStatus(status)));
	}

	private void CloseGattInstances(BluetoothGatt gatt)
	{
		if (!ReferenceEquals(gatt, device.Gatt))
			gatt.Close();

		device.CloseGatt();
	}

	/// <summary>
	/// Not sure why only ConnectionStateChange was checking the parameters originally, but if necessary why don't all overridden callbacks should check it as well.
	/// Likely left over experimental unaware code
	/// </summary>
	private bool ParametersVerified(BluetoothGatt gatt, GattStatus? status = null, [CallerMemberName] string caller = nameof(BluetoothGattCallback))
	{
		Abstractions.Trace.Message($"{caller} {(status != null ? $"{nameof(GattStatus)}: {status}" : string.Empty)}");

		if (gatt?.Device?.Address == null)
		{
			Abstractions.Trace.Message($"{caller} called with null parameters");
			return false;
		}

		if (!gatt.Device.Address.Equals(device.NativeDevice.Address))
		{
			Abstractions.Trace.Message($"{caller} called for device {gatt.Device.Address} having an unmatched underlying device address {device.NativeDevice.Address}");
			return false;
		}

		Abstractions.Trace.Message(gatt.Device.Address);
		return true;
	}

	private static Exception GetExceptionFromGattStatus(GattStatus status)
	{
		return status switch
		{
			GattStatus.Failure
			or GattStatus.InsufficientAuthentication
			or GattStatus.InsufficientEncryption
			or GattStatus.InvalidAttributeLength
			or GattStatus.InvalidOffset
			or GattStatus.ReadNotPermitted
			or GattStatus.RequestNotSupported
			or GattStatus.WriteNotPermitted
			or GattStatus.ConnectionCongested
				=> new($"GattStatus: {(int)status} - {status}"),

			_ when Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu && status == GattStatus.InsufficientAuthorization
				=> new($"GattStatus: {(int)status} - {status}"),

			GattStatus.Success => null,
				_ => new($"GattStatus: {(int)status}")
		};
	}
}