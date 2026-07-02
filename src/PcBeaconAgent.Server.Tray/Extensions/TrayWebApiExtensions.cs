using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace PcBeaconAgent.Server.Tray.Extensions;

public static class TrayWebApiExtensions
{
    public static IServiceCollection AddWebApi(this IServiceCollection services)
    {
#if DEBUG
        services.AddOpenApi();
#endif
        return services;
    }

    public static WebApplication ConfigureWebApi(this WebApplication app)
    {
#if DEBUG
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithOpenApiRoutePattern("/openapi/v1.json");
            options.WithTheme(ScalarTheme.Saturn);
        });
#endif
        return app;
    }
}
