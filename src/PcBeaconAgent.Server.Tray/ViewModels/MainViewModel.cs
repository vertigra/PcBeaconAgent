using CommunityToolkit.Mvvm.ComponentModel;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// Root view model for <see cref="Views.MainWindow"/>. Owns the
    /// three tab view models (Pairing / Settings / Files). All child
    /// VMs are injected via DI — no <c>new</c> in the constructor.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        public PairingViewModel Pairing { get; }
        public SettingsViewModel Settings { get; }
        public FilesViewModel Files { get; }

        public MainViewModel(PairingViewModel pairing, SettingsViewModel settings, FilesViewModel files)
        {
            Pairing = pairing;
            Settings = settings;
            Files = files;
        }

        /// <summary>
        /// Called when the window is shown — refreshes the PIN display
        /// on the Pairing tab so it is not stale.
        /// </summary>
        public void OnWindowShown()
        {
            Pairing.RefreshPin();
        }
    }
}
