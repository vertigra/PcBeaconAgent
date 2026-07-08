using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using PcBeaconAgent.Server.Core.Events;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Tray.Notifications;
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

    public TrayViewModel(
        IPairingService pairingService,
        App app,
        TaskbarIcon trayIcon,
        INotificationService notifications,
        MainViewModel mainViewModel)
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
        // The Expired event is raised from a thread-pool continuation;
        // the others come from the caller's thread (HTTP request for
        // Used/Locked/Generated-via-regenerate, UI thread for
        // Generated via the tray menu's Regenerate). We must marshal
        // to the UI thread before touching any WPF state — in
        // particular IsMainWindowVisible reads Application.Windows,
        // which throws on cross-thread access.
        //
        // INotificationService also marshals internally, but the
        // IsMainWindowVisible check below runs BEFORE we call into
        // the service, so the dispatcher hop has to happen here.
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
                // Suppress both popup and balloon when the user is already
                // looking at the PIN in MainWindow (e.g. they just clicked
                // Regenerate there). External triggers (HTTP
                // /api/pair/regenerate from the Android client) still get
                // the popup because MainWindow is not visible in that flow.
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
    /// viewing the PIN in MainWindow. <b>UI-thread only</b> — reads
    /// <see cref="Application.Windows"/>, which throws on cross-thread
    /// access. Callers must marshal via <see cref="OnPairingStateChanged"/>
    /// before reaching here.
    /// </summary>
    private bool IsMainWindowVisible =>
        mApp.Windows.OfType<Views.MainWindow>().Any(w => w.IsVisible);

    [RelayCommand]
    public void ShowWindow()
    {
        // Left-click on the tray icon (and the "Show window" context menu
        // item) always opens MainWindow — it is the interactive hub (PIN
        // display + Regenerate today, Settings + server status in the
        // future per the roadmap). The popup is a passive notification
        // surface driven by the Generated event, not by user clicks. If
        // the user wants to see the PIN without opening MainWindow, the
        // popup is already on screen (Generated opened it) — they don't
        // need to click anything.
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
            // If the window construction fails (XAML parse error,
            // resource not found, binding exception, etc.) the user
            // would otherwise see nothing — the tray icon stays but
            // no window appears, and the unhandled exception may
            // destabilise the dispatcher. Surface the error so we
            // can diagnose it.
            Log.Fatal(ex, "Failed to open MainWindow");
            MessageBox.Show(
                $"Failed to open main window:\n\n{ex}",
                "PcBeaconAgent",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
