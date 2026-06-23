namespace PcBeaconAgent.Client.Core.Constants
{
    public static class StorageKeys
    {
        public const string KnownDevices = "known_devices";
        public const string DiscoveryPort = "discovery_port";
        public const string ApiKey = "api_key";
        public static string ApiKeyFor(string ipAddress) => $"{ApiKey}:{ipAddress}";
    }
}
