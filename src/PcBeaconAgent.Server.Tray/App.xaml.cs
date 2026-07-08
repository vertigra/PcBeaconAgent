using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Server.Core.BackgroundServices;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Endpoints;
using PcBeaconAgent.Server.Core.Extensions;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Services;
using PcBeaconAgent.Server.Tray.Extensions;
using PcBeaconAgent.Server.Tray.Notifications;
using PcBeaconAgent.Server.Tray.ViewModels;
using PcBeaconAgent.Server.Tray.Views;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace PcBeaconAgent.Server.Tray;

public partial class App : Application
{
    private WebApplication? mWebApp;
    private TrayWindow? mTrayWindow;
    private TrayViewModel? mTrayViewModel;
    private INotificationService? mNotifications;
    private SingleInstanceGuard? mSingleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Acquire the single-instance mutex BEFORE any port bind. If
        // another PcBeaconAgent process (Cli or Tray) is already
        // running, show a friendly message box and exit — without
        // this guard, Kestrel would crash on socket bind with an
        // obscure AddressAlreadyInUse.
        mSingleInstance = new SingleInstanceGuard();
        if (!mSingleInstance.TryAcquire())
        {
            Log.Fatal("Another PcBeaconAgent instance is already running. " +
                      "Exiting. (mutex: {MutexName})", SingleInstanceGuard.MutexName);
            MessageBox.Show(
                "Another PcBeaconAgent instance is already running. " +
                "Please close it first.",
                "PcBeaconAgent",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown(2);
            return;
        }

        try
        {
            await StartWebHostAsync();

            var pairingService = mWebApp!.Services.GetRequiredService<IPairingService>();
            mNotifications = mWebApp.Services.GetRequiredService<INotificationService>();

            mTrayWindow = new TrayWindow();
            mNotifications.AttachTaskbarIcon(mTrayWindow.TrayIcon);

            var mainViewModel = mWebApp.Services.GetRequiredService<MainViewModel>();

            mTrayViewModel = new TrayViewModel(
                pairingService, this, mTrayWindow.TrayIcon, mNotifications, mainViewModel);
            mTrayWindow.DataContext = mTrayViewModel;
            // The window stays hidden — it only hosts the TaskbarIcon.
            mTrayWindow.Show();
            mTrayWindow.Hide();

            _ = mWebApp.Services.GetRequiredService<IPairingService>();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "PcBeaconAgent Tray fatal error on startup");
            MessageBox.Show($"Critical error: {ex.Message}", "PcBeaconAgent",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task StartWebHostAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        AppSettings settings = builder.AddApplicationConfiguration();

        var beaconOptions = new BeaconServerOptions(settings.Server.Host, settings.Server.DiscoveryPort);
        var apiOptions = new WebApiOptions(settings.Server.ApiPort, settings.Server.ApiKey);

        builder.Services.AddSingleton(beaconOptions);
        builder.Services.AddSingleton(apiOptions);

        builder.WebHost.UseUrls($"http://{settings.Server.Host}:{settings.Server.ApiPort}");

        builder.Services.AddBeaconServer();
        builder.Services.AddHostedService<BeaconBackgroundService>();
        builder.Services.AddBeaconServerIdentity();
        builder.Services.AddSignal();
        builder.Services.AddAudioService();
        builder.Services.AddDisplayService();
        builder.Services.AddPairingService();
        builder.Services.AddWebApi();

        // Tray view models and services — singleton so the window state
        // (PIN display, settings) survives open/close cycles within one
        // process run.
        builder.Services.AddSingleton<AppSettings>(settings);
        builder.Services.AddSingleton<Services.IAutoStartService, Services.AutoStartService>();
        // INotificationService factory: Application.Current is set by
        // the time the factory is invoked (resolution happens in
        // OnStartup, after the App constructor has run).
        builder.Services.AddSingleton<INotificationService>(sp => new TrayNotificationService((App)Current));

        builder.Services.AddSingleton<PairingViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<FilesViewModel>();
        builder.Services.AddSingleton<MainViewModel>();

        mWebApp = builder.Build();

        mWebApp.MapSignalHubs();
        mWebApp.ConfigureWebApi();
        mWebApp.MapAudioServiceEndpoints(settings);
        mWebApp.MapDisplayServiceEndpoints(settings);
        mWebApp.MapPairingEndpoints();

        await mWebApp.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        mTrayViewModel?.Dispose();
        // Close any open popup before the app unwinds — otherwise the
        // popup's DispatcherTimer could tick against a disposed window.
        mNotifications?.ClosePinPopup();
        mTrayWindow?.Close();

        if (mWebApp != null)
        {
            await mWebApp.StopAsync();
            await mWebApp.DisposeAsync();
        }

        // Release the single-instance mutex so the next PcBeaconAgent
        // process can start. Must come after the web host stops —
        // otherwise a new instance could acquire the mutex and try to
        // bind ports before this one has released them.
        mSingleInstance?.Dispose();

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
