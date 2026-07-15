using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Server.Core.Interfaces;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Pairing tab. Owns the current PIN display
    /// and exposes the active/empty state for the view to switch
    /// between the PIN display and the "waiting for pairing" cat.
    /// </summary>
    /// <remarks>
    /// <b>PIN lifecycle.</b> The Android client requests a fresh PIN
    /// automatically when it opens the PairingPage (via
    /// <c>/api/pair/regenerate</c>). The server-side
    /// <c>PairingService</c> does not generate a PIN on startup and
    /// must not generate one when the user merely opens the main
    /// window — that would race with the client-driven flow and
    /// produce a PIN the client never asked for. <see cref="RefreshPin"/>
    /// only reads.
    /// </remarks>
    public partial class PairingViewModel : ObservableObject
    {
        private readonly IPairingService mPairingService;

        [ObservableProperty]
        public partial string CurrentPin { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool HasActivePin { get; set; }

        // Inverse of HasActivePin for XAML binding (BooleanToVisibilityConverter
        // does not invert). When there's no active PIN, the cat appears.
        public bool HasNoActivePin => !HasActivePin;

        public PairingViewModel(IPairingService pairingService)
        {
            mPairingService = pairingService;
            RefreshPin();
        }

        /// <summary>
        /// Re-reads the current PIN from the service. Read-only —
        /// does NOT generate a new PIN if none is active. The PIN
        /// display may be empty when the user opens the window before
        /// the Android client has requested one; that is expected and
        /// correct. The cat is shown in that case.
        /// </summary>
        public void RefreshPin()
        {
            CurrentPin = mPairingService.GetCurrentPin();
            HasActivePin = mPairingService.IsPairingActive;
            OnPropertyChanged(nameof(HasNoActivePin));
        }
    }
}
