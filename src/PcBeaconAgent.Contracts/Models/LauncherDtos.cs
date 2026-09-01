using System.Collections.Generic;

namespace PcBeaconAgent.Contracts.Models
{
    /// <summary>
    /// A launcher entry returned by <c>GET /api/launchers</c>.
    /// Contains only the ID and display name — the executable path
    /// is never exposed to the client (security: the client sends
    /// an ID, the server looks up the pre-configured path).
    /// </summary>
    /// <param name="Id">Unique launcher identifier.</param>
    /// <param name="Name">Display name for the Android UI.</param>
    public record LauncherDto(string Id, string Name);

    /// <summary>
    /// Response payload for <c>POST /api/launchers/{id}/launch</c>.
    /// </summary>
    /// <param name="Success"><c>true</c> if the process was started.</param>
    /// <param name="Message">Short status message (e.g. "Launched: steam.exe",
    /// "Launcher not found", "Could not start process").</param>
    /// <param name="Pid">The OS process ID of the launched process, or 0
    /// if the launch failed. Useful for future process-management features.</param>
    public record LaunchResponseDto(bool Success, string Message, int Pid);
}
