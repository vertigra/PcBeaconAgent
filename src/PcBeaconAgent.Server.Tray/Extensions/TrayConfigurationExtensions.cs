using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Server.Core.Configuration;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace PcBeaconAgent.Server.Tray.Extensions;

public static class TrayConfigurationExtensions
{
    public static AppSettings AddApplicationConfiguration(this IHostApplicationBuilder builder)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var settings = new AppSettings();
        configuration.GetSection("ServerSettings").Bind(settings.Server);
        configuration.GetSection("LogSettings").Bind(settings.Log);
        configuration.GetSection("TransferSettings").Bind(settings.Transfer);
        configuration.GetSection("Launchers").Bind(settings.Launchers);
        builder.Services.AddSingleton(settings);

        string fullLogPath = Path.Combine(AppContext.BaseDirectory, settings.Log.FilePath);

        var minimumLevel = ParseMinimumLevel(configuration["Logging:LogLevel:Default"]);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.File(fullLogPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        builder.Services.AddSerilog();

        Log.Information("PcBeaconAgent Tray initialized.");

        return settings;
    }

    private static LogEventLevel ParseMinimumLevel(string? value)
    {
        return (value ?? "Information").Trim().ToLowerInvariant() switch
        {
            "trace" or "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "critical" or "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
