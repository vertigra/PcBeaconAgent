using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Tray.Services;
using System;
using System.Diagnostics;
using System.Reflection;

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

        [ObservableProperty]
        public partial string AppName { get; set; }

        [ObservableProperty]
        public partial string AppVersion { get; set; }

        public SettingsViewModel(IAutoStartService autoStart)
        {
            mAutoStart = autoStart;

            // Read the current state from the registry. If the user
            // previously enabled auto-start, the checkbox reflects it.
            AutoStartEnabled = autoStart.IsEnabled;

            // AppName + AppVersion come from the executing assembly.
            // For publish builds, Version comes from the csproj
            // <Version> property, injected by the CI pipeline.
            Assembly asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            AssemblyName name = asm.GetName();
            AppName = name.Name ?? "PcBeaconAgent.Server.Tray";
            Version? v = name.Version;
            AppVersion = v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";
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
