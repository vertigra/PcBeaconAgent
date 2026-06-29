using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PcBeaconAgent.Client.Core.Models.Common;
using PcBeaconAgent.Client.Core.Services;
using PcBeaconAgent.Service.Configuration;
using PcBeaconAgent.Service.Extensions;
using PcBeaconAgent.Service.JsonContext;
using System;

namespace PcBeaconAgent.Service.Endpoints
{
    public static class DisplayEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapDisplayServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings)
        {
            RouteGroupBuilder displayGroup = app.MapGroup("/api/display").RequireApiKey(settings);

            displayGroup.MapGet("/list", ([FromServices] DisplayController controller) =>
            {
                try
                {
                    return Results.Json(controller.GetDisplays(), ProjectJsonContext.Default.ListDisplayDeviceDto, statusCode: StatusCodes.Status200OK);
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