using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
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
                ShowSecurityWarning(settings);

                builder.WebHost.UseUrls($"http://{settings.Server.Host}:{settings.Server.ApiPort}");

                builder.Services.AddWindowsService(options =>
                {
                    options.ServiceName = "PcBeaconAgent";
                });

                builder.Services.AddHostedService<UdpBeaconServer>();

                builder.Services.AddSignal();
                builder.Services.AddAudioService();
                builder.Services.AddWebApi();

                var app = builder.Build();
                app.MapSignalHubs();
                app.ConfigureWebApi();
                app.MapAudioServiceEndpoints(settings);
                
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
            if (string.IsNullOrEmpty(settings.Server.ApiKey))
            {
                Log.Warning("API Security is DISABLED: No ApiKey configured in ServerSettings.");
                if (Environment.UserInteractive)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("************************************************************");
                    Console.WriteLine("* WARNING: API Security is DISABLED!                       *");
                    Console.WriteLine("* Configure 'ApiKey' in appsettings.json for production.   *");
                    Console.WriteLine("************************************************************");
                    Console.ResetColor();
                }
            }
        }
    }
}