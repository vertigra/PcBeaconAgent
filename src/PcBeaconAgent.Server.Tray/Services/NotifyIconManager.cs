using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Tray.ViewModels;
using PcBeaconAgent.Server.Tray.Views;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PcBeaconAgent.Server.Tray.Services;

public class NotifyIconManager : IDisposable
{
    private readonly TaskbarIcon mTrayIcon;
    private readonly IServiceProvider mServices;
    private bool mDisposed;

    public NotifyIconManager(IServiceProvider services)
    {
        mServices = services;

        mTrayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/beacon.ico", UriKind.Absolute)),
            ToolTipText = "PcBeaconAgent",
            Visibility = Visibility.Visible
        };

        mTrayIcon.TrayMouseDoubleClick += OnTrayMouseDoubleClick;

        // Build context menu via code-behind. Hardcodet's TaskbarIcon
        // supports ContextMenu in XAML, but since the icon is created in
        // code (not XAML), we build the menu here.
        var contextMenu = new ContextMenu();

        var showPinItem = new MenuItem { Header = "Show PIN" };
        showPinItem.Click += (_, _) => ShowPinWindow();
        contextMenu.Items.Add(showPinItem);

        var regenerateItem = new MenuItem { Header = "Regenerate PIN" };
        regenerateItem.Click += (_, _) => RegeneratePin();
        contextMenu.Items.Add(regenerateItem);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Exit();
        contextMenu.Items.Add(exitItem);

        mTrayIcon.ContextMenu = contextMenu;
    }

    public void Show()
    {
        mTrayIcon.Visibility = Visibility.Visible;
    }

    public void ShowNotification(string title, string message)
    {
        mTrayIcon.ShowBalloonTip(title, message, BalloonIcon.Info);
    }

    private void OnTrayMouseDoubleClick(object? sender, RoutedEventArgs e)
    {
        ShowPinWindow();
    }

    public void ShowPinWindow()
    {
        var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        if (mainWindow == null)
        {
            var pairingService = mServices.GetRequiredService<IPairingService>();
            var viewModel = new MainViewModel(pairingService);
            mainWindow = new MainWindow { DataContext = viewModel };
        }

        if (mainWindow.DataContext is MainViewModel vm)
            vm.RefreshPin();

        mainWindow.Show();
        mainWindow.Activate();
    }

    public void RegeneratePin()
    {
        var pairing = mServices.GetService<IPairingService>();
        pairing?.RegeneratePin();

        mTrayIcon.ToolTipText = pairing?.IsPairingActive == true
            ? "PcBeaconAgent — PIN active"
            : "PcBeaconAgent — No active PIN";

        ShowPinWindow();
    }

    public void Exit()
    {
        Application.Current.Shutdown(0);
    }

    public void Dispose()
    {
        if (!mDisposed)
        {
            mTrayIcon.Dispose();
            mDisposed = true;
        }
    }
}
