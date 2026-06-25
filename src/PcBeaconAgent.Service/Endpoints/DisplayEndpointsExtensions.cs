using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Client.Core.Models.Common;
using PcBeaconAgent.Service.Configuration;
using PcBeaconAgent.Service.Extensions;
using PcBeaconAgent.Service.JsonContext;
using PcBeaconAgent.Service.Models;
using PcBeaconAgent.Service.Services;
using System;

namespace PcBeaconAgent.Service.Endpoints
{
    public static class DisplayEndpointsExtensions
    {
        public static IServiceCollection AddDisplayService(this IServiceCollection services)
        {
            services.AddSingleton<DisplayController>();
            return services;
        }

        public static IEndpointRouteBuilder MapDisplayServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings)
        {
            RouteGroupBuilder displayGroup = app.MapGroup("/api/display").RequireApiKey(settings);

            displayGroup.MapGet("/list", ([FromServices] DisplayController controller) =>
            {
                try
                {
                    return Results.Json(controller.GetDisplays(), ServerJsonContext.Default.ListDisplayDeviceDtos, statusCode: StatusCodes.Status200OK);
                }
                catch(Exception ex)
                {
                    return Results.BadRequest(new MessageDto(ex.Message));
                }
            });

            displayGroup.MapPost("/disable", async ([FromBody] DisableRequest request, [FromServices] DisplayController controller) =>
            {
                try
                {
                    await controller.DisableAsync(request.Id);
                    return Results.Json(new MessageDto("Display disabled"), ServerJsonContext.Default.MessageDto, statusCode: StatusCodes.Status200OK);
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
                    return Results.Json(new MessageDto("Displays restored"), ServerJsonContext.Default.MessageDto, statusCode: StatusCodes.Status200OK);
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