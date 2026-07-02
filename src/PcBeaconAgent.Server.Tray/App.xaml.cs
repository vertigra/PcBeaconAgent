using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Server.Core.BackgroundServices;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Endpoints;
using PcBeaconAgent.Server.Core.Extensions;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Tray.Extensions;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace PcBeaconAgent.Server.Tray;

public partial class App : Application
{
    private WebApplication? mWebApp;
    private NotifyIconManager? mTrayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            await StartWebHostAsync();

            mTrayIcon = new NotifyIconManager(mWebApp!.Services);
            mTrayIcon.Show();

            _ = mWebApp.Services.GetRequiredService<IPairingService>();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "PcBeaconAgent Tray fatal error on startup");
            System.Windows.MessageBox.Show($"Critical error: {ex.Message}", "PcBeaconAgent",
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
        mTrayIcon?.Dispose();

        if (mWebApp != null)
        {
            await mWebApp.StopAsync();
            await mWebApp.DisposeAsync();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
