using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PcBeaconAgent.Contracts;
using PcBeaconAgent.Contracts.Models;
using PcBeaconAgent.Server.Core.Interfaces;
using System.Threading.RateLimiting;

namespace PcBeaconAgent.Server.Core.Endpoints
{
    public static class PairingEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapPairingEndpoints(this IEndpointRouteBuilder app)
        {
            var pairingGroup = app.MapGroup("/api/pair");

            pairingGroup.MapPost("/", ([FromBody] PairRequestDto? request, [FromServices] IPairingService pairing) =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.Pin))
                {
                    return Results.Json(
                        new MessageDto("PIN is required."),
                        ProjectJsonContext.Default.MessageDto,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (!pairing.IsPairingActive)
                {
                    return Results.Json(
                        new MessageDto("Pairing mode is not active. PIN may have expired or already been used."),
                        ProjectJsonContext.Default.MessageDto,
                        statusCode: StatusCodes.Status403Forbidden);
                }

                var apiKey = pairing.ValidateAndExchangePin(request.Pin);

                if (apiKey is null)
                {
                    return Results.Json(
                        new MessageDto("Pairing failed."),
                        ProjectJsonContext.Default.MessageDto,
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                return Results.Json(
                    new PairResponseDto(apiKey),
                    ProjectJsonContext.Default.PairResponseDto,
                    statusCode: StatusCodes.Status200OK);
            });

            // Rate-limit regeneration: 1 request per 10 seconds per IP.
            // Prevents DoS (constant regeneration to reset the brute-force
            // counter) and brute-force acceleration.
            pairingGroup.MapPost("/regenerate", ([FromServices] IPairingService pairing) =>
            {
                pairing.RegeneratePin();
                return Results.Json(
                    new MessageDto("New PIN generated. Check the popup next to the server's system tray."),
                    ProjectJsonContext.Default.MessageDto,
                    statusCode: StatusCodes.Status200OK);
            }).RequireRateLimiting("pairing-regenerate");

            return app;
        }
    }
}
