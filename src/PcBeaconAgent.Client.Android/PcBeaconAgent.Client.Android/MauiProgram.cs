using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using PcBeaconAgent.Client.Android.Services;
using PcBeaconAgent.Client.Android.ViewModels;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Services;
using PcBeaconAgent.Client.Core.Stores;

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
        builder.Services.AddHttpClient();

        builder.Services.AddSingleton<IPreferencesService, MauiPreferencesService>();
        builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();

        builder.Services.AddSingleton<DeviceFactory>();
        builder.Services.AddSingleton<DeviceStore>();

        builder.Services.AddSingleton<IUdpBeaconScannerService, UdpBeaconScannerService>();
        builder.Services.AddSingleton<ISignalService, SignalService>();

        builder.Services.AddSingleton<App>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainViewModel>();

        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddTransient<PairingPage>();
        builder.Services.AddTransient<PairingViewModel>();

        builder.Services.AddTransient<AudioControlPage>();
        builder.Services.AddTransient<AudioControlViewModel>();

        return builder.Build();
    }
}