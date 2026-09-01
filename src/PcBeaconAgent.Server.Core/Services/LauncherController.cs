using Microsoft.Extensions.Logging;
using PcBeaconAgent.Server.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PcBeaconAgent.Server.Core.Services
{
    /// <summary>
    /// Reads the configured launcher entries from
    /// <see cref="LaunchersSettings"/> and launches processes on
    /// behalf of the Android client. The client sends a launcher ID
    /// — the server looks up the pre-configured path and starts the
    /// process via <see cref="Process.Start"/>. The client never
    /// sees or sends file system paths.
    /// </summary>
    public sealed class LauncherController
    {
        private readonly LaunchersSettings mSettings;
        private readonly ILogger<LauncherController> mLogger;

        public LauncherController(LaunchersSettings settings, ILogger<LauncherController> logger)
        {
            mSettings = settings;
            mLogger = logger;
        }

        /// <summary>
        /// Returns the list of configured launchers (ID + name only —
        /// no paths exposed to the client).
        /// </summary>
        public IReadOnlyList<(string Id, string Name)> GetLaunchers()
        {
            return mSettings.Entries
                .Select(e => (e.Id, e.Name))
                .ToList();
        }

        /// <summary>
        /// Launches the process identified by <paramref name="id"/>.
        /// The path is looked up from the configured entries — the
        /// caller cannot specify an arbitrary path.
        /// </summary>
        /// <param name="id">Launcher ID from <see cref="GetLaunchers"/>.</param>
        /// <returns>A tuple of (success, message, pid).</returns>
        public (bool Success, string Message, int Pid) Launch(string id)
        {
            var entry = mSettings.Entries.FirstOrDefault(e =>
                string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                LogLauncherNotFound(id);
                return (false, $"Launcher '{id}' not found.", 0);
            }

            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                LogLauncherPathEmpty(entry.Id);
                return (false, $"Launcher '{entry.Name}' has no path configured.", 0);
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = entry.Path,
                    UseShellExecute = true
                };

                if (!string.IsNullOrEmpty(entry.Args))
                    startInfo.Arguments = entry.Args;

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    LogLauncherStartFailed(entry.Id, entry.Path, "Process.Start returned null");
                    return (false, "Could not start the process.", 0);
                }

                int pid = process.Id;
                LogLauncherStarted(entry.Id, entry.Name, entry.Path, pid);
                return (true, $"Launched: {entry.Name}", pid);
            }
            catch (Exception ex)
            {
                LogLauncherStartFailed(entry.Id, entry.Path, ex.Message);
                return (false, $"Could not start '{entry.Name}': {ex.Message}", 0);
            }
        }

        #region Structured logging definitions (allocation-free)

        private static readonly Action<ILogger, string, Exception?> LogLauncherNotFoundAction =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(40, "LauncherNotFound"),
                "Launcher {LauncherId} not found in configuration.");

        private static readonly Action<ILogger, string, Exception?> LogLauncherPathEmptyAction =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(41, "LauncherPathEmpty"),
                "Launcher {LauncherId} has no path configured.");

        private static readonly Action<ILogger, string, string, string, int, Exception?> LogLauncherStartedAction =
            LoggerMessage.Define<string, string, string, int>(
                LogLevel.Information,
                new EventId(42, "LauncherStarted"),
                "Launcher {LauncherId} ({Name}) started: {Path} (PID {Pid}).");

        private static readonly Action<ILogger, string, string, string, Exception?> LogLauncherStartFailedAction =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Error,
                new EventId(43, "LauncherStartFailed"),
                "Launcher {LauncherId} failed to start {Path}: {Error}");

        private void LogLauncherNotFound(string id) =>
            LogLauncherNotFoundAction(mLogger, id, null);

        private void LogLauncherPathEmpty(string id) =>
            LogLauncherPathEmptyAction(mLogger, id, null);

        private void LogLauncherStarted(string id, string name, string path, int pid) =>
            LogLauncherStartedAction(mLogger, id, name, path, pid, null);

        private void LogLauncherStartFailed(string id, string path, string error) =>
            LogLauncherStartFailedAction(mLogger, id, path, error, null);

        #endregion
    }
}
