namespace PcBeaconAgent.Service.Configuration
{
    public class AppSettings
    {
        public ServerSettings Server { get; set; } = new();
        public LogSettings Log { get; set; } = new();
    }

    public class ServerSettings
    {
        public string Host { get; set; } = "0.0.0.0";
        public int ApiPort { get; set; } = 5000;
        public int DiscoveryPort { get; set; } = 8888;
    }

    public class LogSettings
    {
        public string FilePath { get; set; } = "logs\\pc-beacon-agent-.log";
    }
}
