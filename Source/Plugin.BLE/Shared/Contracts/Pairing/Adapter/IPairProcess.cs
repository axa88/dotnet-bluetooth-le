using System;

using Plugin.BLE.Abstractions.Contracts;


namespace Plugin.BLE.Shared.Contracts.Pairing.Adapter;

/// <summary>
/// Indicate the platform's <see cref="IAdapter"/> is able to programmatically process pairing requests
/// </summary>
public interface IPairProcess
{
	public event EventHandler<PairRespondedEventArgs> PairResponded;

	/// <summary>
	/// Arguments returned from a device in response to the pairing request
	/// </summary>
	/// <param name="selectedMode"> The negotiated paring mode selected by the remote device </param>
	/// <param name="approvePairingResponse"> Method to call when the pairing response is accepted </param>
	/// <param name="pin"> Pin provided to application for verification:
	/// <see cref="PairModes.DisplayPin"/> When generated for user display and external verification,
	/// <see cref="PairModes.ConfirmPinMatch"/> When submitted by remote device for user or application verification </param>
	public class PairRespondedEventArgs(PairModes selectedMode, Action<IPairResponse> approvePairingResponse, string pin = null) : System.EventArgs
	{
		public PairModes SelectedMode { get; } = selectedMode;
		public Action<IPairResponse> ApprovePairingResponse { get; } = approvePairingResponse;
		public string Pin { get; } = pin;
	}

	/// <summary>
	/// Use to Accept the pairing response when the negotiated mode either:
	/// <see cref="PairModes.Consent"/>,
	/// <see cref="PairModes.ConfirmPinMatch"/>,
	/// </summary>
	public class ConfirmPairResponse : IPairResponse;

	/// <summary>
	/// Use to Accept a pairing response when the negotiated mode is:
	/// <see cref="PairModes.ProvidePin"/> PIN is provided remotely.
	/// </summary>
	/// <param name="pin"> Set from user input </param>
	public class PinPairResponse(string pin) : IPairResponse
	{
		public string Pin { get; } = pin;
	}

	/// <summary>
	/// Use to Accept a pairing response when the negotiated mode is:
	/// <see cref="PairModes.ProvidePasswordCredential"/>
	/// </summary>
	/// <param name="userName"> The username of the credential. This value must not be null or empty. </param>
	/// <param name="password"> The password string of the credential. This value must not be null or empty</param>
	/// <param name="resource"></param>
	public class CredentialsPairResponse(string userName, string password, string resource = null) : IPairResponse
	{
		public string Password { get; } = password;
		public string UserName { get; } = userName;
		public string Resource { get; } = resource;
	}

	public interface IPairResponse;
}
