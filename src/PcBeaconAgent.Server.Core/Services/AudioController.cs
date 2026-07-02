using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PcBeaconAgent.Server.Core.Services
{
    public class AudioController
    {
        private readonly ILogger<AudioController> mLogger;
        private CoreAudioController? mController;
        private readonly object mControllerLock = new();

        public AudioController(ILogger<AudioController> logger)
        {
            mLogger = logger;
        }

        /// <summary>
        /// Lazily creates and caches the CoreAudioController. The COM
        /// subsystem may need a moment to enumerate playback devices after
        /// the server process starts. If the controller is created too early,
        /// GetPlaybackDevices returns an empty list. We create the controller
        /// on the first call (not in the constructor) and retry the device
        /// enumeration a few times before giving up.
        /// </summary>
        private CoreAudioController GetController()
        {
            lock (mControllerLock)
            {
                mController ??= new CoreAudioController();
                return mController;
            }
        }

        public List<AudioDeviceDto> GetDevices()
        {
            try
            {
                var controller = GetController();

                // Retry the enumeration up to 5 times with 500ms delay.
                // CoreAudioController (COM) can return an empty list on the
                // first call right after process start, before WASAPI
                // finishes initialising the device collection.
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    var devices = controller.GetPlaybackDevices(DeviceState.Active)
                        .Select(d => new AudioDeviceDto(d.Id.ToString(), d.FullName))
                        .ToList();

                    if (devices.Count > 0)
                        return devices;

                    if (attempt < 4)
                        Thread.Sleep(500);
                }

                // Return whatever we got (possibly empty) after all retries.
                return controller.GetPlaybackDevices(DeviceState.Active)
                    .Select(d => new AudioDeviceDto(d.Id.ToString(), d.FullName))
                    .ToList();
            }
            catch (Exception ex)
            {
                LogGetDevicesError(ex);
                throw;
            }
        }

        public DefaultDeviceDto? GetDefaultDevice()
        {
            try
            {
                var device = GetController().DefaultPlaybackDevice;
                return device != null
                    ? new DefaultDeviceDto(device.Id.ToString())
                    : null;
            }
            catch (Exception ex)
            {
                LogGetDefaultDeviceError(ex);
                throw;
            }
        }

        public bool SetDefault(string id)
        {
            try
            {
                var device = GetController().GetPlaybackDevices()
                    .FirstOrDefault(d => d.Id.ToString() == id);

                if (device == null)
                {
                    LogDeviceNotFound(id);
                    return false;
                }

                device.SetAsDefault();
                LogDefaultChanged(id);
                return true;
            }
            catch (Exception ex)
            {
                LogSetDefaultError(id, ex);
                throw;
            }
        }

        #region Structured logging definitions (allocation-free)

        private static readonly Action<ILogger, Exception?> LogGetDevicesErrorAction =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(110, "GetAudioDevicesError"),
                "Failed to retrieve audio device list.");

        private static readonly Action<ILogger, Exception?> LogGetDefaultDeviceErrorAction =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(111, "GetDefaultAudioDeviceError"),
                "Failed to retrieve the default audio device.");

        private static readonly Action<ILogger, string, Exception?> LogDeviceNotFoundAction =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(112, "AudioDeviceNotFound"),
                "Audio device '{DeviceId}' was not found.");

        private static readonly Action<ILogger, string, Exception?> LogDefaultChangedAction =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(113, "DefaultAudioDeviceChanged"),
                "Default audio device changed to '{DeviceId}'.");

        private static readonly Action<ILogger, string, Exception?> LogSetDefaultErrorAction =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(114, "SetDefaultAudioDeviceError"),
                "Failed to set default audio device to '{DeviceId}'.");

        private void LogGetDevicesError(Exception ex) => LogGetDevicesErrorAction(mLogger, ex);
        private void LogGetDefaultDeviceError(Exception ex) => LogGetDefaultDeviceErrorAction(mLogger, ex);
        private void LogDeviceNotFound(string id) => LogDeviceNotFoundAction(mLogger, id, null);
        private void LogDefaultChanged(string id) => LogDefaultChangedAction(mLogger, id, null);
        private void LogSetDefaultError(string id, Exception ex) => LogSetDefaultErrorAction(mLogger, id, ex);

        #endregion
    }
}