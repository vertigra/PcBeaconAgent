using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Service.Endpoints;
using PcBeaconAgent.Service.Extensions;
using PcBeaconAgent.Service.Services;
using Scalar.AspNetCore;
using Serilog;
using System;
using System.Threading.Tasks;

namespace PcBeaconAgent.Service.BackgroundServices
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var builder = WebApplication.CreateSlimBuilder(args);
                var settings = builder.AddApplicationConfiguration(args);

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
                app.MapAudioServiceEndpoints();

                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Critical error on started: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Console.ReadLine();

                Log.Fatal(ex, "PcBeaconAgent service fatal ended");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}