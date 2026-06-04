using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Extensions;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args); 
    var settings = builder.AddApplicationConfiguration(args);

    Log.Information("PcBeaconAgent started...");

    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "PcBeaconAgent";
    });

    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.Urls.Add($"http://{settings.Server.Host}:{settings.Server.Port}");
    app.ConfigureWebApi();

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Critical error on started: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Console.ResetColor();

    Log.Fatal(ex, "PcBeaconAgent service fatal ended");
}
finally
{
    Log.CloseAndFlush();
}