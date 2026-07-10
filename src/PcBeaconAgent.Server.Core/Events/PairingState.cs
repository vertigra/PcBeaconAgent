namespace PcBeaconAgent.Server.Core.Events
{
    /// <summary>
    /// Lifecycle states of the current pairing PIN. Raised via
    /// <see cref="PcBeaconAgent.Server.Core.Interfaces.IPairingService.PairingStateChanged"/>
    /// so UI hosts (tray, console) can react without polling.
    /// </summary>
    public enum PairingState
    {
        /// <summary>A fresh PIN has been generated and is now valid.</summary>
        Generated,

        /// <summary>A client has successfully exchanged the PIN for the ApiKey.</summary>
        Used,

        /// <summary>The PIN reached its expiry window without being used.</summary>
        Expired,

        /// <summary>Too many failed attempts — pairing is locked until restart.</summary>
        Locked
    }
}
