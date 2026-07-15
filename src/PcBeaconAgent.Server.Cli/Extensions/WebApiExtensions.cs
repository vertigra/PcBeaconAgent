using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

namespace PcBeaconAgent.Server.Cli.Extensions
{
    public static class WebApiExtensions
    {
        public static IServiceCollection AddWebApi(this IServiceCollection services)
        {
#if DEBUG
            services.AddOpenApi();
#endif
            // Rate-limit /api/pair/regenerate: 1 request per 10 seconds
            // per IP. Prevents DoS (constant regeneration to reset the
            // brute-force counter).
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
}
