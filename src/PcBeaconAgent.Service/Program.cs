using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using PcBeaconAgent.Client.Core.Configuration;
using PcBeaconAgent.Client.Core.Extensions;
using PcBeaconAgent.Server.Core.Extensions;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Service.BackgroundServices;
using PcBeaconAgent.Service.Configuration;
using PcBeaconAgent.Service.Endpoints;
using PcBeaconAgent.Service.Extensions;
using Serilog;
using System;
using System.Threading.Tasks;

namespace PcBeaconAgent.Service
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var builder = WebApplication.CreateSlimBuilder(args);
                AppSettings settings = builder.AddApplicationConfiguration(args);

                var beaconOptions = new BeaconServerOptions(settings.Server.Host, settings.Server.DiscoveryPort);
                var apiOptions = new WebApiOptions(settings.Server.ApiPort, settings.Server.ApiKey);

                builder.Services.AddSingleton(beaconOptions);
                builder.Services.AddSingleton(apiOptions);

                ShowSecurityWarning(settings);

                builder.WebHost.UseUrls($"http://{settings.Server.Host}:{settings.Server.ApiPort}");

                builder.Services.AddWindowsService(options =>
                {
                    options.ServiceName = "PcBeaconAgent";
                });

                builder.Services.AddBeaconServer();
                builder.Services.AddHostedService<BeaconBackgroundService>();

                builder.Services.AddBeaconServerIdentity();
               
                builder.Services.AddSignal();
                builder.Services.AddAudioService();
                builder.Services.AddDisplayService();
                builder.Services.AddPairingService();
                builder.Services.AddWebApi();

                var app = builder.Build();

                // Force eager instantiation of PairingService so the PIN appears in
                // the log before the first request, not lazily on the first /api/pair call.
                // Without this, a Windows Service running in silent mode would generate
                // the PIN only when the client connects — too late for the user to see it.
                _ = app.Services.GetRequiredService<IPairingService>();

                app.MapSignalHubs();
                app.ConfigureWebApi();
                app.MapAudioServiceEndpoints(settings);
                app.MapDisplayServiceEndpoints(settings);
                app.MapPairingEndpoints();

                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Critical error on started: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
         
                Log.Fatal(ex, "PcBeaconAgent service fatal ended");

                if (!WindowsServiceHelpers.IsWindowsService() && Environment.UserInteractive && !Console.IsInputRedirected)
                {
                    Console.WriteLine("Press Enter to exit...");
                    Console.ReadLine();
                }

                Environment.ExitCode = 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ShowSecurityWarning(AppSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.Server.ApiKey))
            {
                Log.Information("API Security: Using STATIC ApiKey from appsettings.json.");
            }
            else
            {
                Log.Information("API Security: No static key found. Using AUTOMATICALLY generated key (server.key).");
            }
        }
    }
}