using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using PcBeaconAgent.Client.Android.ViewModels;
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

        builder.Services.AddSingleton<IUdpBeaconScanner>(sp =>
        {
            return new UdpBeaconScanner(8888);
        });

        builder.Services.AddSingleton<ISignalRService, SignalRService>();

        builder.Services.AddSingleton<App>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainViewModel>();

        return builder.Build();
    }
}