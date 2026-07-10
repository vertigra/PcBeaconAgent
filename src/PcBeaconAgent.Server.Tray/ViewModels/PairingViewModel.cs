using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Core.Interfaces;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Pairing tab. Owns the current PIN display
    /// and the Regenerate command. The PIN popup lifecycle is owned
    /// by <see cref="TrayViewModel"/> / <c>INotificationService</c> —
    /// this VM is just the in-window display surface.
    /// </summary>
    /// <remarks>
    /// <b>PIN lifecycle.</b> The Android client requests a fresh PIN
    /// automatically when it opens the PairingPage (via
    /// <c>/api/pair/regenerate</c>). The server-side
    /// <c>PairingService</c> does not generate a PIN on startup and
    /// must not generate one when the user merely opens the main
    /// window — that would race with the client-driven flow and
    /// produce a PIN the client never asked for. <see cref="RefreshPin"/>
    /// only reads; <see cref="RegeneratePin"/> is the only path that
    /// writes (and it is bound to an explicit button click).
    /// </remarks>
    public partial class PairingViewModel : ObservableObject
    {
        private readonly IPairingService mPairingService;

        [ObservableProperty]
        public partial string CurrentPin { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasActivePin { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public PairingViewModel(IPairingService pairingService)
        {
            mPairingService = pairingService;
            RefreshPin();
        }

        [RelayCommand]
        public void RegeneratePin()
        {
            IsBusy = true;
            try
            {
                mPairingService.RegeneratePin();
                RefreshPin();
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Re-reads the current PIN from the service. Read-only —
        /// does NOT generate a new PIN if none is active. The PIN
        /// display may be empty when the user opens the window before
        /// the Android client has requested one; that is expected and
        /// correct. The user can click "Regenerate PIN" if they want
        /// one immediately, or just open the Android app's PairingPage
        /// and let it drive.
        /// </summary>
        public void RefreshPin()
        {
            CurrentPin = mPairingService.GetCurrentPin();
            HasActivePin = mPairingService.IsPairingActive;
        }
    }
}

