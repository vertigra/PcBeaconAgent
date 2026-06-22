namespace PcBeaconAgent.Client.Core.Constants
{
    public static class StorageKeys
    {
        public const string KnownDevices = "known_devices";
        public const string DiscoveryPort = "discovery_port";
        public const string ApiKey = "api_key";

        /// <summary>
        /// Строит ключ хранения, привязанный к конкретному серверу по его IP-адресу.
        /// Используйте этот метод везде, где сохраняется/читается ключ после PIN-паринга,
        /// вместо "сырой" константы <see cref="ApiKey"/>.
        /// </summary>
        public static string ApiKeyFor(string ipAddress) => $"{ApiKey}:{ipAddress}";
    }
}
