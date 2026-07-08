using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Tray.Services;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// Root view model for <see cref="Views.MainWindow"/>. Owns the
    /// three tab view models (Pairing / Settings / Files).
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

        public MainViewModel(
            IPairingService pairingService,
            IAutoStartService autoStart)
        {
            mPairing = new PairingViewModel(pairingService);
            Settings = new SettingsViewModel(autoStart);
            Files = new FilesViewModel();
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
