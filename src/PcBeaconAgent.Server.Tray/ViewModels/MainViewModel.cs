using CommunityToolkit.Mvvm.ComponentModel;
using PcBeaconAgent.Server.Core.Services;
using PcBeaconAgent.Server.Tray.ViewModels;
using System;

namespace PcBeaconAgent.Server.Tray.ViewModels
{
    /// <summary>
    /// Root view model for <see cref="Views.MainWindow"/>. Owns the
    /// three tab view models (Pairing / Settings / Files) and the
    /// status bar state (connected clients count).
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly PairingViewModel mPairing;
        private readonly IConnectionTracker mTracker;
        private bool mDisposed;

        public PairingViewModel Pairing => mPairing;
        public SettingsViewModel Settings { get; }
        public FilesViewModel Files { get; }

        [ObservableProperty]
        public partial int ConnectedClients { get; set; }

        public MainViewModel(
            PairingViewModel pairing,
            SettingsViewModel settings,
            FilesViewModel files,
            IConnectionTracker tracker)
        {
            mPairing = pairing;
            Settings = settings;
            Files = files;
            mTracker = tracker;

            // Initialise the count from the current tracker state —
            // the window may open after clients have already connected.
            ConnectedClients = mTracker.ConnectedCount;

            // Subscribe to count changes. The tracker marshals the
            // event onto the WPF Dispatcher (captured at construction
            // time), so the handler runs on the UI thread.
            mTracker.CountChanged += OnCountChanged;
        }

        private void OnCountChanged(int newCount)
        {
            ConnectedClients = newCount;
        }

        /// <summary>
        /// Called when the window is shown — refreshes the PIN display
        /// on the Pairing tab so it is not stale.
        /// </summary>
        public void OnWindowShown()
        {
            mPairing.RefreshPin();
        }

        public void Dispose()
        {
            if (!mDisposed)
            {
                mTracker.CountChanged -= OnCountChanged;
                mDisposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
