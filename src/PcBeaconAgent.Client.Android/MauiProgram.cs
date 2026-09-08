using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using PcBeaconAgent.Client.Android.Pages;
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
        builder.Services.AddSingleton<ReceivedTransferStore>();

        builder.Services.AddSingleton<IBeaconClient, BeaconClient>();
        builder.Services.AddSingleton<ISignalService, SignalService>();
        builder.Services.AddSingleton<IPairingServiceClient, PairingServiceClient>();

        builder.Services.AddSingleton<App>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainViewModel>();

        builder.Services.AddTransient<ReceivedPage>();
        builder.Services.AddTransient<ReceivedViewModel>();

        builder.Services.AddTransient<DiscoveryPage>();
        builder.Services.AddTransient<DiscoveryViewModel>();
        
         builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddTransient<PairingPage>();
        builder.Services.AddTransient<PairingViewModel>();

        builder.Services.AddTransient<AudioControlPage>();
        builder.Services.AddTransient<AudioControlViewModel>();

        builder.Services.AddTransient<DisplayControlPage>();
        builder.Services.AddTransient<DisplayControlViewModel>();

        builder.Services.AddTransient<SendTextPage>();
        builder.Services.AddTransient<SendTextViewModel>();

        builder.Services.AddTransient<AppsPage>();
        builder.Services.AddTransient<AppsViewModel>();

        builder.Services.AddTransient<ShareTextPage>();
        builder.Services.AddTransient<ShareTextViewModel>();

        builder.Services.AddTransient<ShareFilePage>();
        builder.Services.AddTransient<ShareFileViewModel>();

        return builder.Build();
    }
}