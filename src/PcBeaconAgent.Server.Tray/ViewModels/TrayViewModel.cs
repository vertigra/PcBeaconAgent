using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using PcBeaconAgent.Server.Core.Events;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Tray.Services;
using System;
using System.Linq;

namespace PcBeaconAgent.Server.Tray.ViewModels;

/// <summary>
/// ViewModel for the tray icon context menu, single-click action, and
/// PIN lifecycle routing. Subscribes to
/// <see cref="IPairingService.PairingStateChanged"/> and forwards each
/// transition to <see cref="INotificationService"/>. Owns no UI
/// surfaces directly — neither the popup nor the balloons are created
/// here; the service owns them.
/// </summary>
public partial class TrayViewModel : ObservableObject, IDisposable
{
    private readonly IPairingService mPairingService;
    private readonly App mApp;
    private readonly TaskbarIcon mTrayIcon;
    private readonly INotificationService mNotifications;
    private bool mDisposed;

    public TrayViewModel(
        IPairingService pairingService,
        App app,
        TaskbarIcon trayIcon,
        INotificationService notifications)
    {
        mPairingService = pairingService;
        mApp = app;
        mTrayIcon = trayIcon;
        mNotifications = notifications;

        mPairingService.PairingStateChanged += OnPairingStateChanged;
    }

    private void OnPairingStateChanged(PairingStateEventArgs e)
    {
        // The Expired event is raised from a thread-pool continuation;
        // the others come from the caller's thread (HTTP request for
        // Used/Locked, UI thread for Generated via RegeneratePin).
        // INotificationService marshals to the UI thread internally, so
        // we don't need to do it here — just route the event.
        switch (e.State)
        {
            case PairingState.Generated:
                // Suppress both popup and balloon when the user is already
                // looking at the PIN in MainWindow (e.g. they just clicked
                // Regenerate there). External triggers (HTTP
                // /api/pair/regenerate from the Android client, the tray
                // context menu's Regenerate item) still get the popup
                // because MainWindow is not visible in those flows.
                if (IsMainWindowVisible)
                {
                    mTrayIcon.ToolTipText = "PcBeaconAgent — PIN active";
                    break;
                }
                mNotifications.ShowPinPopup(e.Pin, e.ExpiryUtc);
                mTrayIcon.ToolTipText = "PcBeaconAgent — PIN active";
                break;

            case PairingState.Used:
                mNotifications.ClosePinPopup();
                mNotifications.ShowTransient(
                    "Pairing complete",
                    "A client has paired with this PC.",
                    NotificationSeverity.Info);
                mTrayIcon.ToolTipText = "PcBeaconAgent — Paired";
                break;

            case PairingState.Expired:
                mNotifications.ClosePinPopup();
                mNotifications.ShowTransient(
                    "PIN expired",
                    "The pairing PIN was not used and has expired.",
                    NotificationSeverity.Warning);
                mTrayIcon.ToolTipText = "PcBeaconAgent — No active PIN";
                break;

            case PairingState.Locked:
                mNotifications.ClosePinPopup();
                mNotifications.ShowTransient(
                    "Pairing locked",
                    "Too many failed attempts. Restart the service to reset.",
                    NotificationSeverity.Error);
                mTrayIcon.ToolTipText = "PcBeaconAgent — Locked";
                break;
        }
    }

    /// <summary>
    /// True if <see cref="Views.MainWindow"/> is currently on screen.
    /// Used to suppress Generated notifications when the user is already
    /// viewing the PIN in MainWindow.
    /// </summary>
    private bool IsMainWindowVisible =>
        mApp.Windows.OfType<Views.MainWindow>().Any(w => w.IsVisible);

    [RelayCommand]
    public void ShowPin()
    {
        // Left-click on the tray icon always opens MainWindow — it is
        // the interactive hub (PIN display + Regenerate today, Settings
        // + server status in the future per the roadmap). The popup is
        // a passive notification surface driven by the Generated event,
        // not by user clicks. If the user wants to see the PIN without
        // opening MainWindow, the popup is already on screen (Generated
        // opened it) — they don't need to click anything.
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
        // GeneratePin raises the Generated event, which OnPairingStateChanged
        // routes to INotificationService (popup if MainWindow is hidden,
        // nothing if MainWindow is visible — MainWindow's own view model
        // already refreshes its PIN display via RefreshPin on the next
        // user interaction).
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
            mDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
