using System.Threading;
using System.Threading.Tasks;

using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Shared.Contracts.RequestResults;


namespace Plugin.BLE.Shared.Contracts.Pairing.Adapter;

/// <summary>
/// Indicate the platform's <see cref="IAdapter"/> is able to programmatically request Device Bonding
/// </summary>
public interface IBondRequest
{
	/// <summary>
	/// Use to create a Bond with options on Pairing
	/// </summary>
	/// <param name="device"> Device to pair </param>
	/// <param name="bondingOptions"> For use when the platform's <see cref="IAdapter"/> able to accept pairing options </param>
	/// <param name="cancellationToken"> To cancel the Bonding process </param>
	/// <returns></returns>
	public Task<IResult> Bond(IDevice device, BondingOptions bondingOptions = null, CancellationToken cancellationToken = default);
}
