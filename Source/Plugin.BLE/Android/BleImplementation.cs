using System.Threading.Tasks;

using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;

using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Utils;
using Plugin.BLE.BroadcastReceivers;
using Plugin.BLE.Extensions;

using Adapter = Plugin.BLE.Android.Adapter;
using IAdapter = Plugin.BLE.Abstractions.Contracts.IAdapter;


namespace Plugin.BLE
{
	public class BleImplementation : BleImplementationBase
	{
		private static volatile Handler _handler;

		/// <summary>
		/// Set this field to force are task builder execute() actions to be invoked on the main app tread one at a time (synchronous queue)
		/// </summary>
		public static bool ShouldQueueOnMainThread { get; set; } = true;

		private static bool IsMainThread => Looper.MainLooper is { IsCurrentThread: true };

		private BluetoothManager _bluetoothManager;

		protected override void InitializeNative()
		{
			var ctx = Application.Context;
			if (ctx.PackageManager != null && !ctx.PackageManager.HasSystemFeature(PackageManager.FeatureBluetoothLe))
				return;

			var statusChangeReceiver = new BluetoothStatusBroadcastReceiver(state => State = state);
			ctx.RegisterReceiver(statusChangeReceiver, new(BluetoothAdapter.ActionStateChanged));

			_bluetoothManager = (BluetoothManager)ctx.GetSystemService(Context.BluetoothService);

			if (ShouldQueueOnMainThread)
			{
				TaskBuilder.MainThreadInvoker = static action =>
				{

					if (IsMainThread)
						action();
					else
					{
						_handler ??= new(Looper.MainLooper);
						_handler.Post(action);
					}
				};
			}
		}

		protected override BluetoothState GetInitialStateNative() => _bluetoothManager?.Adapter?.State.ToBluetoothState() ?? BluetoothState.Unavailable;

		protected override IAdapter CreateNativeAdapter() => new Adapter(_bluetoothManager);

		public override Task<bool> TrySetStateAsync(bool on)
		{
			const string actionRequestDisable = "android.bluetooth.adapter.action.REQUEST_DISABLE";

			var intent = new Intent(on ? BluetoothAdapter.ActionRequestEnable : actionRequestDisable);
			intent.SetFlags(ActivityFlags.NewTask);
			Application.Context.StartActivity(intent);
			return Task.FromResult(true);
		}
	}
}