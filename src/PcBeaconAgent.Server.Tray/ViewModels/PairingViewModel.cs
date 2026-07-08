using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcBeaconAgent.Server.Core.Interfaces;
using System;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// View model for the Pairing tab. Owns the current PIN display
    /// and the Regenerate command. The PIN popup lifecycle is owned
    /// by <see cref="TrayViewModel"/> / <c>INotificationService</c> —
    /// this VM is just the in-window display surface.
    /// </summary>
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
        /// Re-reads the current PIN from the service. Called by
        /// <see cref="MainViewModel"/> when the user opens the window
        /// (so the display is fresh) and after a Regenerate click.
        /// </summary>
        public void RefreshPin()
        {
            CurrentPin = mPairingService.GetCurrentPin();
            HasActivePin = mPairingService.IsPairingActive;

            // If there's no active PIN when the user opens the window
            // (e.g. they opened it after the previous PIN expired but
            // before the Android client requested a new one), generate
            // one so the UI is not empty. This matches the Tray's
            // overall "PIN-on-demand" model.
            if (!HasActivePin)
            {
                mPairingService.RegeneratePin();
                CurrentPin = mPairingService.GetCurrentPin();
                HasActivePin = mPairingService.IsPairingActive;
            }
        }
    }
}
