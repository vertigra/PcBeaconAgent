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

        // FIX: общий секрет для защиты REST API и SignalR-хаба. По умолчанию пусто
        // (= защита выключена, в лог пишется предупреждение при старте) — чтобы не
        // ломать уже работающие установки без настройки. Для реального использования
        // задайте значение в appsettings.json.
        public string ApiKey { get; set; } = string.Empty;
    }

    public class LogSettings
    {
        public string FilePath { get; set; } = "logs\\pc-beacon-agent-.log";
    }
}
