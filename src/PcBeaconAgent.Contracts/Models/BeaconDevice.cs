namespace PcBeaconAgent.Contracts.Models
{
    /// <summary>
    /// Represents a discovered PC agent device within the local network.
    /// Holds network configuration, hardware identification, and status metrics.
    /// Pure POCO — used as the wire contract between client and server.
    /// </summary>
    public class BeaconDevice
    {
        public string MachineName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int ApiPort { get; set; } = 5000;
        public string MacAddress { get; set; } = string.Empty;
        public string InterfaceName { get; set; } = string.Empty;
        public string InterfaceType { get; set; } = string.Empty;

        public override bool Equals(object? obj) => obj is BeaconDevice device &&
            IpAddress == device.IpAddress &&
            MacAddress == device.MacAddress &&
            InterfaceName == device.InterfaceName &&
            InterfaceType == device.InterfaceType &&
            MachineName == device.MachineName;

        public override int GetHashCode() => IpAddress.GetHashCode();
    }
}