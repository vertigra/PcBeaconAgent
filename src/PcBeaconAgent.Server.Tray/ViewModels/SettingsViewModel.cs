using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Tray.Helpers;
using PcBeaconAgent.Server.Tray.Services;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Settings tab. Exposes the auto-start toggle,
    /// read-only network configuration (Host / API port / Discovery
    /// port), the About section (app name + version + update check),
    /// and the Logs section (log file path + open folder button).
    /// Future settings (network editing, security) will be added here
    /// — see roadmap Tier 2/3 "Settings window".
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

        // Network settings — read-only display. The values come from
        // AppSettings (loaded from appsettings.json by the host). Editing
        // requires a soft restart (Tier 3). Read-only binding →
        // Mode=OneWay in XAML.
        public string Host { get; }
        public int ApiPort { get; }
        public int DiscoveryPort { get; }

        // LogFilePath is read-only display — resolved from AppSettings.Log.FilePath
        // relative to the executable directory. Read-only binding →
        // Mode=OneWay in XAML.
        public string LogFilePath { get; }

        public SettingsViewModel(IAutoStartService autoStart, AppSettings appSettings)
        {
            mAutoStart = autoStart;

            // Read the current state from the registry. If the user
            // previously enabled auto-start, the checkbox reflects it.
            AutoStartEnabled = autoStart.IsEnabled;

            Host = appSettings.Server.Host;
            ApiPort = appSettings.Server.ApiPort;
            DiscoveryPort = appSettings.Server.DiscoveryPort;

            // LogSettings.FilePath is relative (e.g. "logs\pc-beacon.log").
            // Resolve it to an absolute path so the UI shows where the
            // log actually lives on disk.
            string baseDir = System.AppContext.BaseDirectory;
            LogFilePath = Path.GetFullPath(Path.Combine(baseDir, appSettings.Log.FilePath));
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
