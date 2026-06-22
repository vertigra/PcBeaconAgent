namespace PcBeaconAgent.Client.Core.Messages
{
    /// <summary>
    /// Sent by PairingViewModel right after a successful PIN exchange.
    /// Whoever initiated pairing (MainViewModel.Remember, after catching
    /// NotPairedException) listens for this to automatically retry the action
    /// that originally failed, instead of requiring the user to press the
    /// button a second time.
    /// </summary>
    public sealed record PairingSucceededMessage(string IpAddress);
}
