using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PcBeaconAgent.Contracts;
using PcBeaconAgent.Contracts.Models;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Extensions;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Services;

namespace PcBeaconAgent.Server.Core.Endpoints
{
    public static class TransferEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapTransferServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings, IBeaconServerIdentity identity)
        {
            RouteGroupBuilder transferGroup = app.MapGroup("/api/transfer").RequireApiKey(identity);

            // POST /api/transfer/text — accepts a single text payload
            // from the Android client. Rate-limited (10 req/min per IP)
            // to prevent accidental flooding. The API key already
            // authenticates the caller; the rate limit is a courtesy
            // guard against a misbehaving client that spams the endpoint
            // in a loop.
            transferGroup.MapPost("/text", ([FromBody] TextTransferRequestDto? request, [FromServices] TransferController controller, HttpContext context) =>
            {
                if (request is null || string.IsNullOrWhiteSpace(request.Text))
                {
                    return Results.Json(
                        new TextTransferResponseDto(false, "Text payload is required."), ProjectJsonContext.Default.TextTransferResponseDto,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                string sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var (accepted, message) = controller.ReceiveText(request.Text, sourceIp);

                return Results.Json(
                    new TextTransferResponseDto(accepted, message), ProjectJsonContext.Default.TextTransferResponseDto,
                    statusCode: accepted ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
            }).RequireRateLimiting("transfer-text");

            return app;
        }
    }
}
