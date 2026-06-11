using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using PcBeaconAgent.Client.Android.Services;
using PcBeaconAgent.Client.Android.ViewModels;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Services;

namespace PcBeaconAgent.Client.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

        builder.Services.AddLogging();

        builder.Services.AddSingleton<IPreferencesService, MauiPreferencesService>();
        builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();

        builder.Services.AddSingleton<IUdpBeaconScanner>(sp =>
        {
            var prefs = sp.GetRequiredService<IPreferencesService>();
            int port = prefs.Get(StorageKeys.DiscoveryPort, 8888);

            return new UdpBeaconScanner(port);
        });

        builder.Services.AddSingleton<ISignalRService, SignalRService>();
        

        builder.Services.AddSingleton<App>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainViewModel>();

        return builder.Build();
    }
}