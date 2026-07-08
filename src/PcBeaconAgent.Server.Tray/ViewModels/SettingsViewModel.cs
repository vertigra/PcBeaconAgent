using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Tray.Services;
using System.Diagnostics;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Settings tab. Currently exposes the
    /// auto-start toggle and the "About" section (app name + version).
    /// Future settings (network, security, updates, log path) will be
    /// added here — see roadmap Tier 2 "Settings window".
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IAutoStartService mAutoStart;

        [ObservableProperty]
        public partial bool AutoStartEnabled { get; set; }

        [ObservableProperty]
        public partial bool CanChangeAutoStart { get; set; } = true;

        // AppName / AppVersion come from AppInfo — single source of
        // truth shared with MainViewModel. Bound from the About
        // section of SettingsView.
        public string AppName => AppInfo.Name;
        public string AppVersion => AppInfo.Version;

        public SettingsViewModel(IAutoStartService autoStart)
        {
            mAutoStart = autoStart;

            // Read the current state from the registry. If the user
            // previously enabled auto-start, the checkbox reflects it.
            AutoStartEnabled = autoStart.IsEnabled;
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
                // Revert the UI to the registry's actual state. The
                // user sees the checkbox flip back — a soft failure
                // signal. A future improvement would be a small
                // warning popup, but the checkbox revert is enough
                // for now.
                AutoStartEnabled = mAutoStart.IsEnabled;
            }
        }

        [RelayCommand]
        public void OpenProjectUrl()
        {
            // Open the GitHub repo in the default browser. Useful for
            // checking releases / issues / source.
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/vertigra/PcBeaconAgent",
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
