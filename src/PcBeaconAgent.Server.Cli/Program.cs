using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Server.Cli.Extensions;
using PcBeaconAgent.Server.Core.BackgroundServices;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Endpoints;
using PcBeaconAgent.Server.Core.Extensions;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Services;
using Serilog;
using System;
using System.Threading.Tasks;

namespace PcBeaconAgent.Server.Cli
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Acquire the single-instance mutex BEFORE any port bind.
            // If another PcBeaconAgent process (Cli or Tray) is already
            // running, exit immediately with a clear message — without
            // this guard, Kestrel would crash on socket bind with an
            // obscure AddressAlreadyInUse.
            using var singleInstance = new SingleInstanceGuard();
            if (!singleInstance.TryAcquire())
            {
                Log.Fatal("Another PcBeaconAgent instance is already running. " +
                          "Exiting. (mutex: {MutexName})", SingleInstanceGuard.MutexName);

                // The Cli is an interactive console host — there is no
                // service-mode today. The user double-clicked the exe
                // and saw nothing happen because the process exited
                // before they could read the message. Print it to the
                // console and wait for Enter so the window stays open.
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine();
                Console.Error.WriteLine("=================================================");
                Console.Error.WriteLine("  PcBeaconAgent is already running");
                Console.Error.WriteLine("=================================================");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Another PcBeaconAgent instance (Server.Cli or");
                Console.Error.WriteLine("Server.Tray) is already running on this PC.");
                Console.Error.WriteLine("Only one instance can run at a time because they");
                Console.Error.WriteLine("share the same network ports.");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Close the other instance and try again.");
                Console.Error.WriteLine();
                Console.ResetColor();

                if (Environment.UserInteractive && !Console.IsInputRedirected)
                {
                    Console.Error.WriteLine("Press Enter to exit...");
                    Console.ReadLine();
                }

                Environment.ExitCode = 2;
                return;
            }

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

                builder.Services.AddBeaconServer();
                builder.Services.AddHostedService<BeaconBackgroundService>();

                builder.Services.AddBeaconServerIdentity();

                builder.Services.AddSignal();
                builder.Services.AddAudioService();
                builder.Services.AddDisplayService();
                builder.Services.AddPairingService();
                builder.Services.AddTransferService();
                builder.Services.AddWebApi();

                var app = builder.Build();

                // Force eager instantiation of PairingService so its singleton
                // state is ready before the first request. The PIN itself is
                // not generated here — the Android client auto-requests
                // /api/pair/regenerate when PairingPage appears, so a startup
                // PIN would be immediately discarded.
                _ = app.Services.GetRequiredService<IPairingService>();

                app.MapSignalHubs();
                app.ConfigureWebApi();
                var identity = app.Services.GetRequiredService<IBeaconServerIdentity>();
                app.MapAudioServiceEndpoints(settings, identity);
                app.MapDisplayServiceEndpoints(settings, identity);
                app.MapPairingEndpoints();
                app.MapTransferServiceEndpoints(settings, identity);

                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Critical error on started: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();

                Log.Fatal(ex, "PcBeaconAgent service fatal ended");

                if (Environment.UserInteractive && !Console.IsInputRedirected)
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
