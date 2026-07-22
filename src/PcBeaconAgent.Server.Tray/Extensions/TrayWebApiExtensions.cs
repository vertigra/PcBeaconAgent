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

            // 10 text transfers per minute per IP. The API key already
            // authenticates the caller; this is a courtesy guard against
            // a misbehaving client (e.g. a script that spams the
            // endpoint in a loop). 10/min is well above any reasonable
            // manual use — even rapid copy-paste is ~2-3/min.
            options.AddPolicy("transfer-text", context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromSeconds(60),
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
