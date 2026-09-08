using System.Collections.Generic;

namespace PcBeaconAgent.Server.Core.Configuration
{
    public class AppSettings
    {
        public ServerSettings Server { get; set; } = new();
        public LogSettings Log { get; set; } = new();
        public TransferSettings Transfer { get; set; } = new();
        public LaunchersSettings Launchers { get; set; } = new();
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

    /// <summary>
    /// Settings for the app launcher feature. Contains a list of
    /// user-configured executable paths that can be launched from
    /// the Android client. The client only sees launcher IDs and
    /// names — never the file system paths.
    /// </summary>
    public class LaunchersSettings
    {
        public List<LauncherEntry> Entries { get; set; } = [];
    }

    /// <summary>
    /// A single configured launcher entry. The user adds these to
    /// appsettings.json (or a future settings UI) to make apps
    /// available on the Android client.
    /// </summary>
    public class LauncherEntry
    {
        /// <summary>
        /// Unique ID for this launcher. Used by the client to
        /// identify which launcher to invoke.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name shown in the Android client's Apps tab.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the executable, e.g.
        /// <c>C:\Program Files\Steam\steam.exe</c>.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Optional command-line arguments passed to the executable.
        /// </summary>
        public string Args { get; set; } = string.Empty;
    }
}
