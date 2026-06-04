using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Configuration;
using Serilog;
using System.IO;
using System.Linq;

namespace PcBeaconAgent.Extensions;

public static class ConfigurationExtensions
{
    public static AppSettings AddApplicationConfiguration(this IHostApplicationBuilder builder, string[] args)
    {
        var argsList = args?.ToList() ?? [];
        bool silentMode = argsList.Contains("--no-console") || argsList.Contains("--silent");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var settings = new AppSettings();
        configuration.GetSection("ServerSettings").Bind(settings.Server);
        configuration.GetSection("LogSettings").Bind(settings.Log);
        builder.Services.AddSingleton(settings);

        string fullLogPath = Path.Combine(AppContext.BaseDirectory, settings.Log.FilePath);
        
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
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
}