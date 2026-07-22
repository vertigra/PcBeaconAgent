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
    public static class AudioEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapAudioServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings, IBeaconServerIdentity identity)
        {
            var audioGroup = app.MapGroup("/api/audio").RequireApiKey(identity);

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