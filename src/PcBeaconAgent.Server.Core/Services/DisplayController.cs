using Microsoft.Extensions.Logging;
using PcBeaconAgent.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsDisplayAPI.DisplayConfig;
using WindowsDisplayAPI.Native.DisplayConfig;

namespace PcBeaconAgent.Server.Core.Services
{
    public class DisplayController
    {
        private readonly ILogger<DisplayController> mLogger;
        private DisplayConfigTopologyId mActiveDefaultUserTopology = PathInfo.GetCurrentTopology();
        private bool mIsTopologyOverridden;
        private readonly SemaphoreSlim mDisplayLock = new(1, 1);

        public DisplayController(ILogger<DisplayController> logger)
        {
            mLogger = logger;
        }

        public List<DisplayDeviceDto> GetDisplays()
        {
            try
            {
                HashSet<PathDisplayTarget> activeTargets = [.. PathInfo.GetActivePaths()
                    .SelectMany(p => p.TargetsInfo)
                    .Select(t => t.DisplayTarget)];

                return [.. PathDisplayTarget.GetDisplayTargets()
                    .Where(t => t.IsAvailable)
                    .Select(t => new DisplayDeviceDto(
                        Id: t.DevicePath,
                        FriendlyName: t.FriendlyName,
                        IsActive: activeTargets.Contains(t)))];
            }
            catch (Exception ex)
            {
                LogGetDisplaysError(ex);
                throw;
            }
        }

        public async Task DisableAsync(string devicePath)
        {
            await mDisplayLock.WaitAsync();

            try
            {
                LogDisablingDisplay(devicePath);

                var activePaths = PathInfo.GetActivePaths();

                if (!mIsTopologyOverridden)
                {
                    mActiveDefaultUserTopology = PathInfo.GetCurrentTopology();
                    mIsTopologyOverridden = true;
                }

                PathInfo targetPath = activePaths.FirstOrDefault(p => p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == devicePath))
                    ?? throw new InvalidOperationException($"Display '{devicePath}' not found.");

                if (targetPath.TargetsInfo.Length > 1 && PathInfo.GetCurrentTopology() == DisplayConfigTopologyId.Clone)
                {
                    PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: true);

                    int retries = 5;
                    while (PathInfo.GetCurrentTopology() != DisplayConfigTopologyId.Extend && retries > 0)
                    {
                        await Task.Delay(500);
                        retries--;
                    }

                    if (PathInfo.GetCurrentTopology() != DisplayConfigTopologyId.Extend)
                    {
                        throw new InvalidOperationException(
                            "Could not switch from Clone to Extend topology within the timeout.");
                    }
                }

                DisableByDevicePath(devicePath);

                LogDisplayDisabled(devicePath);
            }
            catch (Exception ex)
            {
                LogDisplayError(devicePath, ex);
                throw;
            }
            finally
            {
                mDisplayLock.Release();
            }
        }

        private static void DisableByDevicePath(string devicePath)
        {
            var activePaths = PathInfo.GetActivePaths();

            var remaining = activePaths
                .Where(p => !p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == devicePath))
                .ToArray();

            if (remaining.Length == activePaths.Length)
            {
                throw new InvalidOperationException(
                    $"Display '{devicePath}' was not found among active paths after topology adjustment.");
            }

            // Windows rejects applying an empty path set (ApplyPathInfos throws
            // PathChangeException: "Invalid paths information"). At least one
            // display must remain active, so refuse the operation explicitly
            // with a clear message instead of letting the native call fail.
            if (remaining.Length == 0)
            {
                throw new InvalidOperationException(
                    "Cannot disable the last active display. At least one display must remain active.");
            }

            PathInfo.ApplyPathInfos(remaining, allowChanges: true, saveToDatabase: true);
        }

        public async Task RestoreAll()
        {
            await mDisplayLock.WaitAsync();
            try
            {
                LogRestoringTopology(mActiveDefaultUserTopology.ToString());
                PathInfo.ApplyTopology(mActiveDefaultUserTopology, allowPersistence: true);

                mIsTopologyOverridden = false;
            }
            catch (Exception ex)
            {
                LogRestoreError(ex);
                throw;
            }
            finally
            {
                mDisplayLock.Release();
            }
        }


        private static readonly Action<ILogger, Exception?> LogGetDisplaysErrorAction =
            LoggerMessage.Define(LogLevel.Error, new EventId(100, "GetDisplaysError"), "Failed to retrieve display list.");

        private static readonly Action<ILogger, string, Exception?> LogDisablingDisplayAction =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(101, "DisablingDisplay"), "Attempting to disable display: {DevicePath}");

        private static readonly Action<ILogger, string, Exception?> LogDisplayDisabledAction =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(102, "DisplayDisabled"), "Display {DevicePath} successfully disabled.");

        private static readonly Action<ILogger, string, Exception?> LogDisplayErrorAction =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(103, "DisplayError"), "Error processing display {DevicePath}");

        private static readonly Action<ILogger, string, Exception?> LogRestoringTopologyAction =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(104, "RestoringTopology"), "Restoring display topology to {Topology}");

        private static readonly Action<ILogger, Exception?> LogRestoreErrorAction =
            LoggerMessage.Define(LogLevel.Error, new EventId(105, "RestoreError"), "Failed to restore display topology.");


        private void LogGetDisplaysError(Exception ex) => LogGetDisplaysErrorAction(mLogger, ex);
        private void LogDisablingDisplay(string path) => LogDisablingDisplayAction(mLogger, path, null);
        private void LogDisplayDisabled(string path) => LogDisplayDisabledAction(mLogger, path, null);
        private void LogDisplayError(string path, Exception ex) => LogDisplayErrorAction(mLogger, path, ex);
        private void LogRestoringTopology(string topology) => LogRestoringTopologyAction(mLogger, topology, null);
        private void LogRestoreError(Exception ex) => LogRestoreErrorAction(mLogger, ex);

    }
}