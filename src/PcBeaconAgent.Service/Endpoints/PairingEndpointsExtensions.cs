using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Service.Interfaces;
using PcBeaconAgent.Service.Services;
using System.Text.Json.Serialization;

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
            // This endpoint is intentionally NOT protected by RequireApiKey —
            // the client does not have the key yet; PIN is the proof of physical access.
            // Security is provided by: short TTL, single-use PIN, lockout after 5 failures.
            app.MapPost("/api/pair", (
                [FromBody] PairRequest request,
                [FromServices] IPairingService pairing) =>
            {
                if (!pairing.IsPairingActive)
                    return Results.Problem(
                        detail: "Pairing mode is not active. PIN may have expired or already been used.",
                        statusCode: StatusCodes.Status403Forbidden);

                var apiKey = pairing.ValidateAndExchangePin(request.Pin);

                if (apiKey is null)
                    return Results.Problem(
                        detail: "Invalid PIN or pairing locked due to too many failed attempts.",
                        statusCode: StatusCodes.Status401Unauthorized);

                return Results.Ok(new PairResponse(apiKey));
            });

            // Allows the user to request a fresh PIN without restarting the service
            // (e.g., the old one expired). Requires no auth — same reasoning as above.
            app.MapPost("/api/pair/regenerate", ([FromServices] IPairingService pairing) =>
            {
                pairing.RegeneratePin();
                return Results.Ok(new { message = "New PIN generated. Check the server console." });
            });

            return app;
        }
    }

    public record PairRequest(string Pin);
    public record PairResponse(string ApiKey);
}