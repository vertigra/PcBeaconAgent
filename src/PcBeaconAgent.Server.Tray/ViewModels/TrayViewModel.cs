using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using PcBeaconAgent.Server.Core.Events;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Tray.Models;
using PcBeaconAgent.Server.Tray.Services;
using Serilog;
using System;
using System.Linq;
using System.Windows;

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
    private readonly MainViewModel mMainViewModel;
    private bool mDisposed;

    public TrayViewModel(IPairingService pairingService, App app, TaskbarIcon trayIcon, INotificationService notifications, MainViewModel mainViewModel)
    {
        mPairingService = pairingService;
        mApp = app;
        mTrayIcon = trayIcon;
        mNotifications = notifications;
        mMainViewModel = mainViewModel;

        mPairingService.PairingStateChanged += OnPairingStateChanged;
    }

    private void OnPairingStateChanged(PairingStateEventArgs e)
    {
        // Use BeginInvoke (asynchronous) instead of Invoke (synchronous)
        // so the calling thread — typically an HTTP request thread —
        // is not blocked. Blocking the HTTP thread would delay the
        // response to the client (buttons stay disabled) and risks
        // deadlock if the UI thread tries to acquire a lock that the
        // HTTP thread holds.
        if (!mApp.Dispatcher.CheckAccess())
        {
            mApp.Dispatcher.BeginInvoke(new Action(() => HandleStateChange(e)));
            return;
        }
        HandleStateChange(e);
    }

    private void HandleStateChange(PairingStateEventArgs e)
    {
        switch (e.State)
        {
            case PairingState.Generated:
                // When MainWindow is open, suppress the popup (the user
                // is already looking at the PIN) but refresh the Pairing
                // tab so the display shows the new PIN instead of the
                // stale one. When MainWindow is closed, show the popup.
                if (IsMainWindowVisible)
                {
                    mMainViewModel.Pairing.RefreshPin();
                    mTrayIcon.ToolTipText = "PcBeaconAgent — PIN active";
                    break;
                }
                mNotifications.ShowPinPopup(e.Pin, e.ExpiryUtc);
                mTrayIcon.ToolTipText = "PcBeaconAgent — PIN active";
                break;

            case PairingState.Used:
                mNotifications.ClosePinPopup();
                if (IsMainWindowVisible)
                    mMainViewModel.Pairing.RefreshPin();
                mNotifications.ShowTransient(
                    "Pairing complete",
                    "A client has paired with this PC.",
                    NotificationSeverity.Info);
                mTrayIcon.ToolTipText = "PcBeaconAgent — Paired";
                break;

            case PairingState.Expired:
                mNotifications.ClosePinPopup();
                if (IsMainWindowVisible)
                    mMainViewModel.Pairing.RefreshPin();
                mNotifications.ShowTransient(
                    "PIN expired",
                    "The pairing PIN was not used and has expired.",
                    NotificationSeverity.Warning);
                mTrayIcon.ToolTipText = "PcBeaconAgent — No active PIN";
                break;

            case PairingState.Locked:
                mNotifications.ClosePinPopup();
                if (IsMainWindowVisible)
                    mMainViewModel.Pairing.RefreshPin();
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
    /// viewing the PIN in MainWindow. <b>UI-thread only</b> — reads
    /// <see cref="Application.Windows"/>, which throws on cross-thread
    /// access. Callers must marshal via <see cref="OnPairingStateChanged"/>
    /// before reaching here.
    /// </summary>
    private bool IsMainWindowVisible =>
        mApp.Windows.OfType<Views.MainWindow>().Any(w => w.IsVisible);

    private bool mWindowOpening;

    [RelayCommand]
    public void ShowWindow()
    {
        // Guard against rapid double-click creating two MainWindows.
        // Both ShowWindow calls would find no existing window and both
        // would create a new one. The flag is set before the check and
        // cleared after Show() completes (synchronous on the UI thread).
        if (mWindowOpening) return;
        mWindowOpening = true;
        try
        {
            var mainWindow = mApp.Windows.OfType<Views.MainWindow>().FirstOrDefault();
            if (mainWindow == null)
            {
                mainWindow = new Views.MainWindow { DataContext = mMainViewModel };
            }

            mMainViewModel.OnWindowShown();

            mainWindow.Show();
            mainWindow.Activate();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to open MainWindow");
            MessageBox.Show(
                $"Failed to open main window:\n\n{ex}",
                "PcBeaconAgent",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            mWindowOpening = false;
        }
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
