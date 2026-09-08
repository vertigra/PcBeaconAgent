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
using System.Linq;

namespace PcBeaconAgent.Server.Core.Endpoints
{
    public static class LauncherEndpointsExtensions
    {
        public static IEndpointRouteBuilder MapLauncherServiceEndpoints(this IEndpointRouteBuilder app, AppSettings settings, IBeaconServerIdentity identity)
        {
            RouteGroupBuilder group = app.MapGroup("/api/launchers").RequireApiKey(identity);

            // GET /api/launchers — returns the list of configured
            // launchers (ID + name only, no paths).
            group.MapGet("/", ([FromServices] LauncherController controller) =>
            {
                var launchers = controller.GetLaunchers()
                    .Select(l => new LauncherDto(l.Id, l.Name))
                    .ToList();

                return Results.Json(launchers, ProjectJsonContext.Default.ListLauncherDto,
                    statusCode: StatusCodes.Status200OK);
            });

            // POST /api/launchers/{id}/launch — launches the process
            // identified by the launcher ID. The path is looked up
            // from the server-side configuration — the client cannot
            // specify an arbitrary path.
            group.MapPost("/{id}/launch", ([FromRoute] string id, [FromServices] LauncherController controller) =>
            {
                var (success, message, pid) = controller.Launch(id);

                return Results.Json(
                    new LaunchResponseDto(success, message, pid),
                    ProjectJsonContext.Default.LaunchResponseDto,
                    statusCode: success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
            });

            return app;
        }
    }
}
