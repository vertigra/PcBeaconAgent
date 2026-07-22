namespace PcBeaconAgent.Server.Core.Configuration
{
    public class AppSettings
    {
        public ServerSettings Server { get; set; } = new();
        public LogSettings Log { get; set; } = new();
        public TransferSettings Transfer { get; set; } = new();
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

    /// <summary>
    /// Settings for the cross-device transfer feature. Files received
    /// from the Android client are saved to <see cref="SaveFolder"/>.
    /// The folder is created on first use if it does not exist.
    /// </summary>
    public class TransferSettings
    {
        /// <summary>
        /// Folder where received files are saved. May contain the
        /// <c>%USERPROFILE%</c> placeholder, which is expanded to
        /// <c>Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)</c>
        /// at runtime. Default is the user's Downloads subfolder.
        /// </summary>
        public string SaveFolder { get; set; } = "%USERPROFILE%\\Downloads\\PcBeaconAgent";
    }
}
