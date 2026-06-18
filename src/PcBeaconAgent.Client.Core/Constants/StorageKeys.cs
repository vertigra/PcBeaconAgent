namespace PcBeaconAgent.Client.Core.Constants
{
    public static class StorageKeys
    {
        public const string KnownDevices = "known_devices";
        public const string DiscoveryPort = "discovery_port";

        // FIX: ключ хранения общего секрета для аутентификации на сервере.
        public const string ApiKey = "api_key";
    }
}
