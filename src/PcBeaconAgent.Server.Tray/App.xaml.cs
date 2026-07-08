using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Server.Core.BackgroundServices;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Endpoints;
using PcBeaconAgent.Server.Core.Extensions;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Services;
using PcBeaconAgent.Server.Tray.Extensions;
using PcBeaconAgent.Server.Tray.Notifications;
using PcBeaconAgent.Server.Tray.Services;
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
    // AppSettings is read from appsettings.json in StartWebHostAsync
    // and registered as a DI singleton so the Settings tab can read
    // the network configuration without re-reading the file.
    private AppSettings? mAppSettings;

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
            // INotificationService is constructed before the TaskbarIcon
            // exists — the icon lives in TrayWindow.xaml and is only
            // available after InitializeComponent. We pass it in via
            // AttachTaskbarIcon below. (Patch C will move this into DI.)
            mNotifications = new TrayNotificationService(this);

            mTrayWindow = new TrayWindow();
            mNotifications.AttachTaskbarIcon(mTrayWindow.TrayIcon);

            // MainViewModel is resolved from DI — its child VMs
            // (Pairing, Settings, Files) are resolved transitively
            // with their own dependencies (IPairingService,
            // IAutoStartService, AppSettings).
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
        mAppSettings = builder.AddApplicationConfiguration();
        AppSettings settings = mAppSettings;

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

        // Tray view models — singleton so the window state (PIN display,
        // settings) survives open/close cycles within one process run.
        // AppSettings and IAutoStartService are registered here so the
        // child VMs can resolve them transitively. INotificationService
        // stays manual until patch C (it needs the App instance, which
        // is not available at DI registration time).
        builder.Services.AddSingleton<AppSettings>(settings);
        builder.Services.AddSingleton<PcBeaconAgent.Server.Tray.Services.IAutoStartService,
            PcBeaconAgent.Server.Tray.Services.AutoStartService>();
        builder.Services.AddSingleton<PcBeaconAgent.Server.Tray.ViewModels.PairingViewModel>();
        builder.Services.AddSingleton<PcBeaconAgent.Server.Tray.ViewModels.SettingsViewModel>();
        builder.Services.AddSingleton<PcBeaconAgent.Server.Tray.ViewModels.FilesViewModel>();
        builder.Services.AddSingleton<PcBeaconAgent.Server.Tray.ViewModels.MainViewModel>();

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
