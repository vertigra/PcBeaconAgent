namespace PcBeaconAgent.Server.Cli.Configuration
{
    public class AppSettings
    {
        public ServerSettings Server { get; set; } = new();
        public LogSettings Log { get; set; } = new();
    }

    public class ServerSettings
    {
        /// <summary>
        /// beacon server settings
        /// </summary>
        public string Host { get; set; } = "0.0.0.0";

        /// <summary>
        /// beacon server settings
        /// </summary>
        public int DiscoveryPort { get; set; } = 8888;

        /// <summary>
        /// WebApi settings
        /// </summary>
        public int ApiPort { get; set; } = 5000;

        /// <summary>
        /// WebApi settings
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }

    public class LogSettings
    {
        public string FilePath { get; set; } = "logs\\pc-beacon-agent-.log";
    }
}
