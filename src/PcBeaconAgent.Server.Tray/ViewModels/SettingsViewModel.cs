using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Tray.Services;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Settings tab. Currently exposes the
    /// auto-start toggle, the About section (app name + version +
    /// update check), and the Logs section (log file path + open
    /// folder button). Future settings (network, security) will be
    /// added here — see roadmap Tier 2/3 "Settings window".
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IAutoStartService mAutoStart;

        [ObservableProperty]
        public partial bool AutoStartEnabled { get; set; }

        [ObservableProperty]
        public partial bool CanChangeAutoStart { get; set; } = true;

        // AppName / AppVersion come from AppInfo — single source of
        // truth shared with MainWindow title. Bound from the About
        // section of SettingsView.
        public string AppName => AppInfo.Name;
        public string AppVersion => AppInfo.Version;

        // LogFilePath is read-only display — the path comes from
        // appsettings.json (LogSettings.FilePath) and is resolved
        // to an absolute path relative to the executable. Read-only
        // binding → Mode=OneWay in XAML.
        public string LogFilePath { get; }

        public SettingsViewModel(IAutoStartService autoStart)
        {
            mAutoStart = autoStart;

            // Read the current state from the registry. If the user
            // previously enabled auto-start, the checkbox reflects it.
            AutoStartEnabled = autoStart.IsEnabled;

            // Resolve the log file path. We read appsettings.json
            // directly here rather than injecting AppSettings because
            // the DI container in App.xaml.cs builds the web host
            // separately and does not expose AppSettings to the tray
            // view models. This is a small read at window-open time;
            // if the file is missing or malformed, we fall back to a
            // sane default.
            LogFilePath = ResolveLogFilePath();
        }

        /// <summary>
        /// Reads <c>LogSettings.FilePath</c> from <c>appsettings.json</c>
        /// and resolves it to an absolute path relative to the
        /// executable directory. Returns a fallback path if the
        /// configuration cannot be read.
        /// </summary>
        private static string ResolveLogFilePath()
        {
            try
            {
                string baseDir = System.AppContext.BaseDirectory;
                string configPath = Path.Combine(baseDir, "appsettings.json");
                if (!File.Exists(configPath))
                    return Path.Combine(baseDir, "logs", "pcbeacon.log");

                string json = File.ReadAllText(configPath);
                // Lightweight extraction — avoids pulling in
                // Microsoft.Extensions.Configuration just for one
                // string. The JSON structure is:
                //   "LogSettings": { "FilePath": "logs\\…" }
                int idx = json.IndexOf("\"FilePath\"", System.StringComparison.Ordinal);
                if (idx < 0) return Path.Combine(baseDir, "logs", "pcbeacon.log");
                int colon = json.IndexOf(':', idx);
                int openQuote = json.IndexOf('"', colon + 1);
                int closeQuote = json.IndexOf('"', openQuote + 1);
                if (openQuote < 0 || closeQuote < 0)
                    return Path.Combine(baseDir, "logs", "pcbeacon.log");

                string relative = json.Substring(openQuote + 1, closeQuote - openQuote - 1);
                return Path.GetFullPath(Path.Combine(baseDir, relative));
            }
            catch
            {
                return "logs/pcbeacon.log";
            }
        }

        /// <summary>
        /// Called by the XAML binding when the user toggles the
        /// checkbox. Writes through to the registry immediately —
        /// save-on-change semantics, no Apply button. The checkbox
        /// state is the source of truth; if the registry write fails,
        /// we flip the checkbox back so the UI stays consistent.
        /// </summary>
        partial void OnAutoStartEnabledChanged(bool value)
        {
            if (!CanChangeAutoStart) return;

            bool ok = mAutoStart.SetEnabled(value);
            if (!ok)
            {
                AutoStartEnabled = mAutoStart.IsEnabled;
            }
        }

        [RelayCommand]
        public void OpenProjectUrl()
        {
            OpenUrl("https://github.com/vertigra/PcBeaconAgent");
        }

        [RelayCommand]
        public void CheckForUpdates()
        {
            // Auto-update is a Tier 3 feature. For now, the button
            // opens the GitHub Releases page in the default browser
            // so the user can check manually.
            OpenUrl("https://github.com/vertigra/PcBeaconAgent/releases");
        }

        [RelayCommand]
        public void OpenLogFolder()
        {
            try
            {
                // Open the folder containing the log file in Explorer.
                // If the folder does not exist yet (no logs written),
                // create it so Explorer doesn't show an error.
                string? folder = Path.GetDirectoryName(LogFilePath);
                if (string.IsNullOrEmpty(folder))
                {
                    Log.Warning("Log folder path is empty: {Path}", LogFilePath);
                    return;
                }

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                Log.Warning(ex, "Failed to open log folder: {Path}", LogFilePath);
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Shell launch can fail in locked-down environments.
                // Non-fatal — the user can copy the URL manually.
            }
        }
    }
}
