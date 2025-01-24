using System;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin.BLE.Abstractions.Utils;

/// <summary>
/// Builder class to create event driven Tasks that may be marshalled onto the main thread.
/// </summary>
public static class TaskBuilder
{
	/// <summary>
	/// Platform specific main thread invocation. Useful to avoid GATT 133 errors on Android.
	/// Set this to NULL in order to disable main thread queued invocations.
	/// Android: already implemented and set by default
	/// Windows, iOS, macOS: NULL by default - not needed, turning this on is redundant as it's already handled internally by the platform
	/// </summary>
	public static Action<Action> MainThreadInvoker { get; set; }

	/// <summary>
	/// Main thread queue semaphore timeout
	/// </summary>
	private static int SemaphoreQueueTimeout => 30;

	private static readonly SemaphoreSlim QueueSemaphore = new(1);

	/// <summary>
	/// Creates an event driven chain of <see cref="Action">Actions</see>.
	/// </summary>
	public static async Task<TReturn> FromEvent<TReturn, TEventHandler, TRejectHandler>(
		Action execute,
		Func<Action<TReturn>, Action<Exception>, TEventHandler> getCompleteHandler,
		Action<TEventHandler> subscribeComplete,
		Action<TEventHandler> unsubscribeComplete,
		Func<Action<Exception>, TRejectHandler> getRejectHandler,
		Action<TRejectHandler> subscribeReject,
		Action<TRejectHandler> unsubscribeReject,
		CancellationToken token = default,
		bool mainThread = true)
	{
		TaskCompletionSource<TReturn> tcs = new();
		void Complete(TReturn args) => tcs.TrySetResult(args);
		void CompleteException(Exception ex) => tcs.TrySetException(ex);
		void Reject(Exception ex) => tcs.TrySetException(ex);

		var handler = getCompleteHandler(Complete, CompleteException);
		var rejectHandler = getRejectHandler(Reject);

		try
		{
			subscribeComplete(handler);
			subscribeReject(rejectHandler);
			using (token.Register(() => tcs.TrySetCanceled(), false))
			{
				return await SafeEnqueueAndExecute(execute, token, tcs, mainThread).ConfigureAwait(false);
			}
		}
		finally
		{
			unsubscribeReject(rejectHandler);
			unsubscribeComplete(handler);
		}
	}

	/// <summary>
	/// Queues the given <see cref="Action"/> onto the main thread and executes it.
	/// </summary>
	public static Task EnqueueOnMainThreadAsync(Action execute, CancellationToken token = default, bool mainThread = true) => SafeEnqueueAndExecute<bool>(execute, token, mainThread: mainThread);

	private static async Task<TReturn> SafeEnqueueAndExecute<TReturn>(Action execute, CancellationToken token, TaskCompletionSource<TReturn> tcs = null, bool mainThread = true)
	{
		var shouldCompleteTask = tcs == null;
		tcs ??= new();
		var shouldReleaseSemaphore = false;

		try
		{
			if (MainThreadInvoker != null && mainThread)
			{
				if (await QueueSemaphore.WaitAsync(TimeSpan.FromSeconds(SemaphoreQueueTimeout), token).ConfigureAwait(false))
				{
					shouldReleaseSemaphore = true;
					MainThreadInvoker.Invoke(() =>
					{
						try
						{
							execute();
							if (shouldCompleteTask)
								tcs.TrySetResult(default);
						}
						catch (Exception ex) { tcs.TrySetException(ex); }
					});
				}
				else
					tcs.TrySetCanceled(token);
			}
			else
			{
				token.ThrowIfCancellationRequested();
				execute();
				tcs.TrySetResult(default);
			}

			return await tcs.Task.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			tcs.TrySetCanceled(token);
			throw;
		}
		catch (Exception ex)
		{
			tcs.TrySetException(ex);
			throw;
		}
		finally
		{
			if (shouldReleaseSemaphore)
				QueueSemaphore.Release();
		}
	}
}