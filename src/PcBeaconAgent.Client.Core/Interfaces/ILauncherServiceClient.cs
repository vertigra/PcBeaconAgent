using PcBeaconAgent.Contracts.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Interfaces
{
    /// <summary>
    /// Client-side accessor for the server's launcher endpoints
    /// (<c>GET /api/launchers</c> and
    /// <c>POST /api/launchers/{id}/launch</c>). Created per-device by
    /// <see cref="Stores.DeviceFactory"/> and exposed on
    /// <see cref="Models.ManagedDevice.Launcher"/>.
    /// </summary>
    public interface ILauncherServiceClient
    {
        /// <summary>
        /// Returns the list of configured launchers on the server.
        /// Contains only IDs and display names — no file system paths.
        /// </summary>
        Task<IReadOnlyList<LauncherDto>> GetLaunchersAsync();

        /// <summary>
        /// Launches the process identified by <paramref name="id"/>
        /// on the server. The path is looked up from the server-side
        /// configuration.
        /// </summary>
        /// <param name="id">Launcher ID from <see cref="GetLaunchersAsync"/>.</param>
        /// <returns>The server's response with success/failure, message,
        /// and PID.</returns>
        Task<LaunchResponseDto> LaunchAsync(string id);
    }
}
