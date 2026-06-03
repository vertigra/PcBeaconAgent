using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Configuration;
using Serilog;

namespace PcBeaconAgent.Extensions
{
    public static class ConfigurationExtensions
    {
        public static AppSettings AddApplicationConfiguration(this IHostApplicationBuilder builder)
        {
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
                .WriteTo.File(fullLogPath, rollingInterval: RollingInterval.Day);

#if DEBUG
            loggerConfig.WriteTo.Console();
#endif
            Log.Logger = loggerConfig.CreateLogger();

            return settings;
        }
    }
}