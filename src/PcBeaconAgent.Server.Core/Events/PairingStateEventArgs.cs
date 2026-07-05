using System;

namespace PcBeaconAgent.Server.Core.Events
{
    /// <summary>
    /// Payload for
    /// <see cref="PcBeaconAgent.Server.Core.Interfaces.IPairingService.PairingStateChanged"/>.
    /// PIN and ExpiryUtc are populated for <see cref="PairingState.Generated"/>;
    /// for terminal states they are empty/default (the PIN is no longer
    /// valid and should not be displayed).
    /// </summary>
    public sealed class PairingStateEventArgs : EventArgs
    {
        public PairingState State { get; init; }
        public string Pin { get; init; } = string.Empty;
        public DateTime ExpiryUtc { get; init; }
    }
}
