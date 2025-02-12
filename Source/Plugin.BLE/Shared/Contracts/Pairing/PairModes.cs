using System;

using Plugin.BLE.Shared.Contracts.Pairing.Adapter;


namespace Plugin.BLE.Shared.Contracts.Pairing;

[Flags]
public enum PairModes
{
	/// <summary> No pairing is supported. </summary>
	None = 0,

	/// <summary>
	/// It is intended that the application confirms that the user wishes to perform the pairing action. An optional confirmation dialog can be presented to the UI.
	/// The application must respond via <see cref="IPairProcess.PairRespondedEventArgs.ApprovePairingResponse"/> with <see cref="IPairProcess.ConfirmPairResponse"/> if the pairing is to complete.
	/// </summary>
	Consent = 0b1,

	/// <summary>
	/// It is intended that the application displays the given PIN so the user can enter it on the other device.
	/// The application must respond via <see cref="IPairProcess.PairRespondedEventArgs.ApprovePairingResponse"/> with <see cref="IPairProcess.ConfirmPairResponse"/> if the pairing is to complete.
	/// </summary>
	DisplayPin = 0b10,

	/// <summary>
	/// It is intended that the application requests from the user, either a known or remotely displayed PIN.
	/// The application must respond via <see cref="IPairProcess.PairRespondedEventArgs.ApprovePairingResponse"/> passing the <see cref="IPairProcess.PinPairResponse.Pin"/> via <see cref="IPairProcess.PinPairResponse"/> if the pairing is to complete.
	/// </summary>
	ProvidePin = 0b100,

	/// <summary>
	/// It is intended that the application displays and allows the user to confirm the PIN matches on both devices.
	/// The application must respond via <see cref="IPairProcess.PairRespondedEventArgs.ApprovePairingResponse"/> with <see cref="IPairProcess.ConfirmPairResponse"/> if the pairing is to complete.
	/// </summary>
	ConfirmPinMatch = 0b1000,

	/// <summary>
	/// It is intended that the application request a username and password from the user.
	/// The application must respond via <see cref="IPairProcess.PairRespondedEventArgs.ApprovePairingResponse"/> with <see cref="IPairProcess.CredentialsPairResponse"/> if the pairing is to complete.
	/// </summary>
	ProvidePasswordCredential = 0B1_0000,

	/// <summary>
	/// Represents any of the available pairing methods.
	/// Intended for use when the application is prepared to process any negotiated pairing mode
	/// </summary>
	Any = ProvidePasswordCredential | ConfirmPinMatch | ProvidePin | DisplayPin | Consent
}
