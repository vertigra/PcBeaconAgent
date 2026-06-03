using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PcBeaconAgent.Endpoints
{
    public static class AudioEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapAudioEndpoints(this IEndpointRouteBuilder app)
        {
            // Группируем все эндпоинты под общим префиксом /api/audio
            var audioGroup = app.MapGroup("/api/audio");

            audioGroup.MapGet("/favicon.ico", () => Results.NoContent());

            audioGroup.MapGet("/devices", (CoreAudioController controller) =>
            {
                var devices = controller.GetPlaybackDevices(DeviceState.Active).Select(d => new { d.Id, d.FullName });
                return Results.Ok(devices);
            });

            audioGroup.MapGet("/default-device", (CoreAudioController controller) =>
            {
                var device = controller.DefaultPlaybackDevice;
                return device != null ? Results.Ok(new { id = device.Id.ToString() }) : Results.NotFound();
            });

            audioGroup.MapPost("/set", (string id, CoreAudioController controller) =>
            {
                var device = controller.GetPlaybackDevices().FirstOrDefault(d => d.Id.ToString() == id);
                if (device != null)
                {
                    device.SetAsDefault();
                    return Results.Ok(new { message = "Default device changed" });
                }
                return Results.NotFound();
            });

            return app;
        }
    }
}
