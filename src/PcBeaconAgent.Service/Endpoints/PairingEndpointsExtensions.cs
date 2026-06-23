using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Service.Interfaces;
using PcBeaconAgent.Service.JsonContext;
using PcBeaconAgent.Service.Models;
using PcBeaconAgent.Service.Services;

namespace PcBeaconAgent.Service.Endpoints
{
    public static class PairingEndpointsExtensions
    {
        public static IServiceCollection AddPairingService(this IServiceCollection services)
        {
            services.AddSingleton<IPairingService, PairingService>();
            return services;
        }

        public static IEndpointRouteBuilder MapPairingEndpoints(this IEndpointRouteBuilder app)
        {
            var pairingGroup = app.MapGroup("/api/pair");

            pairingGroup.MapPost("/", ([FromBody] PairRequest request, [FromServices] IPairingService pairing) =>
            {
                if (!pairing.IsPairingActive)
                {
                    return Results.Json(
                        new SimpleMessageResponse("Pairing mode is not active. PIN may have expired or already been used."),
                        BeaconJsonContext.Default.SimpleMessageResponse,
                        statusCode: StatusCodes.Status403Forbidden);
                }

                var apiKey = pairing.ValidateAndExchangePin(request.Pin);

                if (apiKey is null)
                {
                    return Results.Json(
                        new SimpleMessageResponse("Invalid PIN or pairing locked due to too many failed attempts."),
                        BeaconJsonContext.Default.SimpleMessageResponse,
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                return Results.Json(
                    new PairResponse(apiKey),
                    BeaconJsonContext.Default.PairResponse,
                    statusCode: StatusCodes.Status200OK);
            });

            pairingGroup.MapPost("/regenerate", ([FromServices] IPairingService pairing) =>
            {
                pairing.RegeneratePin();
                return Results.Json(
                    new SimpleMessageResponse("New PIN generated. Check the server console."),
                    BeaconJsonContext.Default.SimpleMessageResponse,
                    statusCode: StatusCodes.Status200OK);
            });

            return app;
        }
    }
}
