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
using System;

namespace PcBeaconAgent.Server.Core.Endpoints
{
    public static class DisplayEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapDisplayServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings, IBeaconServerIdentity identity)
        {
            RouteGroupBuilder displayGroup = app.MapGroup("/api/display").RequireApiKey(identity);

            displayGroup.MapGet("/list", ([FromServices] DisplayController controller) =>
            {
                try
                {
                    return Results.Json(controller.GetDisplays(), ProjectJsonContext.Default.DisplayListResponseDto, statusCode: StatusCodes.Status200OK);
                }
                catch(Exception ex)
                {
                    return Results.BadRequest(new MessageDto(ex.Message));
                }
            });

            displayGroup.MapPost("/disable", async ([FromBody] DisableRequestDto request, [FromServices] DisplayController controller) =>
            {
                try
                {
                    await controller.DisableAsync(request.Id);
                    return Results.Json(new MessageDto("Display disabled"), ProjectJsonContext.Default.MessageDto, statusCode: StatusCodes.Status200OK);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new MessageDto(ex.Message));
                }
            });

            displayGroup.MapPost("/restore", async ([FromServices] DisplayController controller) =>
            {
                try
                {
                    await controller.RestoreAll();
                    return Results.Json(new MessageDto("Displays restored"), ProjectJsonContext.Default.MessageDto, statusCode: StatusCodes.Status200OK);
                }
                catch(Exception ex)
                {
                    return Results.BadRequest(new MessageDto(ex.Message));
                }
            });

            return app;
        }
    }
}