using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Extensions;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 1. Читаем конфиг и запускаем логгер
    var settings = builder.AddApplicationConfiguration();

    // 2. Подключаем Serilog к ASP.NET
    builder.Host.UseSerilog();

    Log.Information("PcBeaconAgent вошел в фарватер и запускается...");

    // 3. Настраиваем работу в качестве службы Windows
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "PcBeaconAgent";
    });

    builder.Services.AddOpenApi();

    var app = builder.Build();

    // 4. Задаем динамический адрес из настроек
    app.Urls.Add($"http://{settings.Server.Host}:{settings.Server.Port}");

    // 5. ВЫЗЫВАЕМ ВАШЕ РАСШИРЕНИЕ:
    // Оно само разрулит #if DEBUG для Scalar и привяжет MapAudioEndpoints()
    app.ConfigureWebApi();

    // 6. Поехали!
    await app.RunAsync();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Критическая ошибка при старте хоста: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Console.ResetColor();

    Log.Fatal(ex, "Критическая авария на маяке! Служба PcBeaconAgent аварийно завершилась.");
}
finally
{
    Log.CloseAndFlush();
}