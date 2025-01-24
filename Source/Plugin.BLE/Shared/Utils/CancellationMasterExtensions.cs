using System.Threading;

namespace Plugin.BLE.Shared.Utils;

/// <summary>
/// Extensions for <c>ICancellationMaster</c>.
/// </summary>
public static class CancellationMasterExtensions
{
	/// <summary>
	/// Obtain a combined token source of the <c>ICancellationMaster</c> with any other taken.
	/// </summary>
	public static CancellationTokenSource GetCombinedSource(this ICancellationMaster cancellationMaster, CancellationToken token) => CancellationTokenSource.CreateLinkedTokenSource(cancellationMaster.TokenSource.Token, token);

	/// <summary>
	/// Cancel any task connected to the token and dispose the source.
	/// </summary>
	public static void CancelEverything(this ICancellationMaster cancellationMaster)
	{
		cancellationMaster.TokenSource?.Cancel();
		cancellationMaster.TokenSource?.Dispose();
		cancellationMaster.TokenSource = null;
	}

	/// <summary>
	/// Cancel any task connected to the token and create a new source.
	/// </summary>
	public static void CancelEverythingAndReInitialize(this ICancellationMaster cancellationMaster)
	{
		cancellationMaster.CancelEverything();
		cancellationMaster.TokenSource = new();
	}
}


/// <summary>
/// Base class for anything that needs a cancellation token source.
/// </summary>
public interface ICancellationMaster
{
	/// <summary>
	/// The cancellation token source.
	/// </summary>
	CancellationTokenSource TokenSource { get; set; }
}