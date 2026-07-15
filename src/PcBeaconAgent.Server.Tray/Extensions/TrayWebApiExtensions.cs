using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using System;
using System.Threading.RateLimiting;

namespace PcBeaconAgent.Server.Tray.Extensions;

public static class TrayWebApiExtensions
{
    public static IServiceCollection AddWebApi(this IServiceCollection services)
    {
#if DEBUG
        services.AddOpenApi();
#endif
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("pairing-regenerate", context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromSeconds(10),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }

    public static WebApplication ConfigureWebApi(this WebApplication app)
    {
        app.UseRateLimiter();

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
