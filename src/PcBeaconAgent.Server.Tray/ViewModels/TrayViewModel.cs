using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Tray.Views;
using System;
using System.Linq;

namespace PcBeaconAgent.Server.Tray.ViewModels;

/// <summary>
/// ViewModel for the tray icon context menu, single-click action and
/// PIN lifecycle notifications. Subscribes to
/// <see cref="IPairingService.PairingStateChanged"/> to drive a persistent
/// popup on <see cref="PairingState.Generated"/> (PIN + countdown) and
/// short transient balloons on the terminal states
/// (<see cref="PairingState.Used"/>, <see cref="PairingState.Expired"/>,
/// <see cref="PairingState.Locked"/>).
/// </summary>
public partial class TrayViewModel : ObservableObject, IDisposable
{
    private readonly IPairingService mPairingService;
    private readonly App mApp;
    private readonly TaskbarIcon mTrayIcon;
    private PinPopupWindow? mActivePopup;
    private bool mDisposed;

    public TrayViewModel(IPairingService pairingService, App app, TaskbarIcon trayIcon)
    {
        mPairingService = pairingService;
        mApp = app;
        mTrayIcon = trayIcon;

        mPairingService.PairingStateChanged += OnPairingStateChanged;
    }

    private void OnPairingStateChanged(PairingStateEventArgs e)
    {
        // The Expired event is raised from a thread-pool continuation;
        // the others come from the caller's thread (HTTP request for
        // Used/Locked, UI thread for Generated via RegeneratePin).
        // Always marshal to the UI thread — balloon/popup APIs require it.
        if (!mApp.Dispatcher.CheckAccess())
        {
            mApp.Dispatcher.Invoke(() => HandleStateChange(e));
            return;
        }
        HandleStateChange(e);
    }

    private void HandleStateChange(PairingStateEventArgs e)
    {
        switch (e.State)
        {
            case PairingState.Generated:
                // The PIN is fresh — open the persistent popup so the user
                // can read it for the entire validity window. The popup
                // has its own countdown and self-closes at expiry.
                ShowPopup(e.Pin, e.ExpiryUtc);
                mTrayIcon.ToolTipText = "PcBeaconAgent — PIN active";
                break;

            case PairingState.Used:
                ClosePopup();
                ShowBalloon("Pairing complete",
                    "A client has paired with this PC.", BalloonIcon.Info);
                mTrayIcon.ToolTipText = "PcBeaconAgent — Paired";
                break;

            case PairingState.Expired:
                ClosePopup();
                ShowBalloon("PIN expired",
                    "The pairing PIN was not used and has expired.",
                    BalloonIcon.Warning);
                mTrayIcon.ToolTipText = "PcBeaconAgent — No active PIN";
                break;

            case PairingState.Locked:
                ClosePopup();
                ShowBalloon("Pairing locked",
                    "Too many failed attempts. Restart the service to reset.",
                    BalloonIcon.Error);
                mTrayIcon.ToolTipText = "PcBeaconAgent — Locked";
                break;
        }
    }

    private void ShowPopup(string pin, DateTime expiryUtc)
    {
        ClosePopup();

        mActivePopup = new Views.PinPopupWindow(pin, expiryUtc);
        // Auto-clear our reference when the user closes the popup manually
        // (or when its own countdown timer elapses). Without this, the next
        // Generated event would try to Close() an already-closed window.
        mActivePopup.Closed += OnPopupClosed;
        mActivePopup.Show();
    }

    private void ClosePopup()
    {
        if (mActivePopup != null)
        {
            // Detach first so our manual Close() doesn't re-enter OnPopupClosed
            // — keeps the lifecycle explicit and avoids a double-null.
            mActivePopup.Closed -= OnPopupClosed;
            mActivePopup.Close();
            mActivePopup = null;
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        mActivePopup = null;
    }

    private void ShowBalloon(string title, string message, BalloonIcon icon)
    {
        // Hardcodet 2.x dropped the customTimeout parameter — Windows
        // ignores it anyway and clamps to its own system timeout (~10–30s).
        // The balloon is intentionally a short transient signal, not a
        // persistent state holder — that's what the popup is for.
        mTrayIcon.ShowBalloonTip(title, message, icon);
    }

    [RelayCommand]
    public void ShowPin()
    {
        // If a PIN is currently active, surface it via the popup with the
        // real remaining lifetime so the countdown is accurate. Otherwise
        // open the full MainWindow where the user can request a new PIN.
        string currentPin = mPairingService.GetCurrentPin();
        DateTime? expiry = mPairingService.GetCurrentPinExpiryUtc();
        if (!string.IsNullOrEmpty(currentPin) && expiry.HasValue)
        {
            ShowPopup(currentPin, expiry.Value);
            return;
        }

        var mainWindow = mApp.Windows.OfType<Views.MainWindow>().FirstOrDefault();
        if (mainWindow == null)
        {
            var viewModel = new MainViewModel(mPairingService);
            mainWindow = new Views.MainWindow { DataContext = viewModel };
        }

        if (mainWindow.DataContext is MainViewModel vm)
            vm.RefreshPin();

        mainWindow.Show();
        mainWindow.Activate();
    }

    [RelayCommand]
    public void RegeneratePin()
    {
        // GeneratePin raises the Generated event, which opens the popup.
        // No need to call ShowPin() explicitly — the event handler does it.
        mPairingService.RegeneratePin();
    }

    [RelayCommand]
    public void Exit()
    {
        mApp.Shutdown(0);
    }

    public void Dispose()
    {
        if (!mDisposed)
        {
            mPairingService.PairingStateChanged -= OnPairingStateChanged;
            ClosePopup();
            mDisposed = true;
        }
    }
}
