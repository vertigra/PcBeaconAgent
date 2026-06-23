using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PcBeaconAgent.Service.Configuration;
using PcBeaconAgent.Service.Extensions;
using PcBeaconAgent.Service.JsonContext;
using PcBeaconAgent.Service.Models;
using System.Linq;

namespace PcBeaconAgent.Service.Endpoints
{
    public static class AudioEndpointsExtensions
    {
        public static IServiceCollection AddAudioService(this IServiceCollection services)
        {
            services.AddSingleton<CoreAudioController>();
            return services;
        }

        public static IEndpointRouteBuilder MapAudioServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings)
        {
            var audioGroup = app.MapGroup("/api/audio").RequireApiKey(settings);

            audioGroup.MapGet("/devices", ([FromServices] CoreAudioController controller) =>
            {
                var devices = controller.GetPlaybackDevices(DeviceState.Active)
                                        .Select(d => new AudioDeviceDto(d.Id.ToString(), d.FullName))
                                        .ToList();

                return Results.Json(devices, BeaconJsonContext.Default.ListAudioDeviceDto);
            });

            audioGroup.MapGet("/default-device", ([FromServices] CoreAudioController controller) =>
            {
                var device = controller.DefaultPlaybackDevice;
                return device != null
                    ? Results.Json(new DefaultDeviceResponse(device.Id.ToString()), BeaconJsonContext.Default.DefaultDeviceResponse)
                    : Results.NotFound();
            });

            audioGroup.MapPost("/set", ([FromQuery] string id, [FromServices] CoreAudioController controller) =>
            {
                var device = controller.GetPlaybackDevices().FirstOrDefault(d => d.Id.ToString() == id);
                if (device != null)
                {
                    device.SetAsDefault();
                    return Results.Json(new MessageResponse("Default device changed"), BeaconJsonContext.Default.MessageResponse);
                }

                return Results.NotFound();
            });

            return app;
        }
    }
}