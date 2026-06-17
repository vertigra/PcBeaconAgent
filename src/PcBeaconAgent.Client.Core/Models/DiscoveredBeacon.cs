namespace PcBeaconAgent.Client.Core.Models
{
    public class DiscoveredBeacon
    {
        /// <summary>
        /// The IP address of the discovered agent.
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// The API port on which the agent is listening for HTTP requests.
        /// </summary>
        public int Port { get; set; }
    }
}