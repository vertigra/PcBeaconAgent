using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Tray.Services;
using System;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// Root view model for <see cref="Views.MainWindow"/>. Owns the
    /// three tab view models (Pairing / Settings / Files) and exposes
    /// the header state (app name + version + icon source).
    /// </summary>
    /// <remarks>
    /// The tab VMs are constructed once when the window is first
    /// opened and reused for the lifetime of the window — closing
    /// the window does not dispose them, but re-opening reuses the
    /// same instance. This is intentional: settings made on the
    /// Settings tab should persist across window open/close cycles
    /// within a single process run.
    /// </remarks>
    public partial class MainViewModel : ObservableObject
    {
        private readonly PairingViewModel mPairing;

        public PairingViewModel Pairing => mPairing;
        public SettingsViewModel Settings { get; }
        public FilesViewModel Files { get; }

        [ObservableProperty]
        public partial string AppName { get; set; }

        [ObservableProperty]
        public partial string AppVersion { get; set; }

        public MainViewModel(
            IPairingService pairingService,
            IAutoStartService autoStart)
        {
            mPairing = new PairingViewModel(pairingService);
            Settings = new SettingsViewModel(autoStart);
            Files = new FilesViewModel();

            // Header text. Mirrors SettingsViewModel.AppName/Version —
            // we duplicate the lookup rather than sharing a service
            // because the values are static for the process lifetime
            // and the indirection would be overkill.
            System.Reflection.Assembly asm =
                System.Reflection.Assembly.GetEntryAssembly()
                ?? System.Reflection.Assembly.GetExecutingAssembly();
            System.Reflection.AssemblyName name = asm.GetName();
            AppName = name.Name ?? "PcBeaconAgent.Server.Tray";
            System.Version? v = name.Version;
            AppVersion = v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";
        }

        /// <summary>
        /// Called by the window's Loaded handler (and by
        /// <c>TrayViewModel.ShowWindow</c>) to refresh the PIN display
        /// when the window opens. Passes through to the Pairing tab.
        /// </summary>
        public void OnWindowShown()
        {
            mPairing.RefreshPin();
        }
    }
}
