using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PcBeaconAgent.Client.Core.JsonContext;
using PcBeaconAgent.Client.Core.Models.Common;
using PcBeaconAgent.Server.Core.Services;
using PcBeaconAgent.Service.Configuration;
using PcBeaconAgent.Service.Extensions;

namespace PcBeaconAgent.Service.Endpoints
{
    public static class AudioEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapAudioServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings)
        {
            var audioGroup = app.MapGroup("/api/audio").RequireApiKey(settings);

            audioGroup.MapGet("/devices", ([FromServices] AudioController controller) =>
            {
                var devices = controller.GetDevices();
                return Results.Json(devices, ProjectJsonContext.Default.ListAudioDeviceDto, statusCode: StatusCodes.Status200OK);
            });

            audioGroup.MapGet("/default-device", ([FromServices] AudioController controller) =>
            {
                var device = controller.GetDefaultDevice();
                return device != null
                     ? Results.Json(device, ProjectJsonContext.Default.DefaultDeviceDto, statusCode: StatusCodes.Status200OK)
                       : Results.NotFound();
            });
       

            audioGroup.MapPost("/set", ([FromQuery] string id, [FromServices] AudioController controller) =>
            {
                var changed = controller.SetDefault(id);
                if (changed)
                {
                    return Results.Json(new MessageDto("Default device changed"), ProjectJsonContext.Default.MessageDto, statusCode: StatusCodes.Status200OK);
                }

                return Results.NotFound();
            });

            return app;
        }
    }
}