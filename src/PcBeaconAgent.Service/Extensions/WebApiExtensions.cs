using Microsoft.AspNetCore.Builder;
using PcBeaconAgent.Service.Endpoints;
using Scalar.AspNetCore;

namespace PcBeaconAgent.Service.Extensions
{
    public static class WebApiExtensions
    {
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
            app.MapAudioEndpoints();

            return app;
        }
    }
}
