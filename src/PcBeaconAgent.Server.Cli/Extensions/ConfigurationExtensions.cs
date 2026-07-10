using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Server.Core.Configuration;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Linq;

namespace PcBeaconAgent.Server.Cli.Extensions;

public static class ConfigurationExtensions
{
    public static AppSettings AddApplicationConfiguration(this IHostApplicationBuilder builder, string[] args)
    {
        var argsList = args?.ToList() ?? [];
        bool silentMode = argsList.Contains("--no-console") || argsList.Contains("--silent");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var settings = new AppSettings();
        configuration.GetSection("ServerSettings").Bind(settings.Server);
        configuration.GetSection("LogSettings").Bind(settings.Log);
        builder.Services.AddSingleton(settings);

        string fullLogPath = Path.Combine(AppContext.BaseDirectory, settings.Log.FilePath);

        var minimumLevel = ParseMinimumLevel(configuration["Logging:LogLevel:Default"]);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.File(fullLogPath, rollingInterval: RollingInterval.Day);

        if (!silentMode)
        {
            loggerConfig.WriteTo.Console();
        }

        Log.Logger = loggerConfig.CreateLogger();

        builder.Services.AddSerilog();

        Log.Information("PcBeaconAgent initialized. Silent mode: {SilentMode}", silentMode);

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