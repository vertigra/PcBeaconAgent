using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using PcBeaconAgent.Server.Core.Interfaces;
using System;
using System.Linq;
using System.Windows;

namespace PcBeaconAgent.Server.Tray.ViewModels;

/// <summary>
/// ViewModel for the tray icon context menu and balloon notifications.
/// Subscribes to IPairingService.PairingStateChanged to show/hide
/// balloon notifications when a PIN is generated, used, or expired.
/// </summary>
public partial class TrayViewModel : ObservableObject, IDisposable
{
    private readonly IPairingService mPairingService;
    private readonly App mApp;
    private readonly TaskbarIcon mTrayIcon;
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
        // Marshal to the UI thread — the event is raised from a background
        // thread (Task.Delay continuation or HTTP request thread).
        mApp.Dispatcher.Invoke(() =>
        {
            switch (e.State)
            {
                case PairingState.Generated:
                    mTrayIcon.ShowBalloonTip(
                        "Pairing PIN Generated",
                        $"PIN: {e.Pin}\nValid for {(e.ExpiryUtc - DateTime.UtcNow).TotalMinutes:F0} minutes",
                        BalloonIcon.Info);
                    mTrayIcon.ToolTipText = "PcBeaconAgent — PIN active";
                    break;

                case PairingState.Used:
                    mTrayIcon.ShowBalloonTip(
                        "Pairing Successful",
                        "A client has paired with this PC.",
                        BalloonIcon.Info);
                    mTrayIcon.ToolTipText = "PcBeaconAgent — Paired";
                    break;

                case PairingState.Expired:
                    mTrayIcon.ShowBalloonTip(
                        "PIN Expired",
                        "The pairing PIN was not used and has expired.",
                        BalloonIcon.Warning);
                    mTrayIcon.ToolTipText = "PcBeaconAgent — No active PIN";
                    break;

                case PairingState.Locked:
                    mTrayIcon.ShowBalloonTip(
                        "Pairing Locked",
                        "Too many failed attempts. Restart the service to reset.",
                        BalloonIcon.Error);
                    mTrayIcon.ToolTipText = "PcBeaconAgent — Locked";
                    break;
            }
        });
    }

    [RelayCommand]
    public void ShowPin()
    {
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
        mPairingService.RegeneratePin();
        ShowPin();
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
        }
    }
}
