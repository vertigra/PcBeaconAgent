using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Configuration;
using PcBeaconAgent.Server.Core.Interfaces;
using System;
using System.IO;

namespace PcBeaconAgent.Server.Core.Services
{
    internal class BeaconServerIdentity : IBeaconServerIdentity
    {
        private readonly ILogger<BeaconServerIdentity> mLogger;

        public string ApiKey { get; }
        public int ApiPort { get; }

        public BeaconServerIdentity(WebApiOptions webApiOptions, ILogger<BeaconServerIdentity> logger)
        {
            mLogger = logger;
            ApiPort = webApiOptions.ApiPort;

            if (!string.IsNullOrEmpty(webApiOptions.ApiKey))
            {
                ApiKey = webApiOptions.ApiKey;
                LogKeyLoaded("AppSettings", "Configuration");
            }
            else
            {
                ApiKey = LoadOrCreateKey("server.key");
            }
        }

        private string LoadOrCreateKey(string keyPath)
        {
            try
            {
                if (File.Exists(keyPath))
                {
                    var key = File.ReadAllText(keyPath).Trim();
                    LogKeyLoaded("file", keyPath);
                    return key;
                }

                var newKey = Guid.NewGuid().ToString("N");
                File.WriteAllText(keyPath, newKey);
                LogKeyGenerated(keyPath);
                return newKey;
            }
            catch (Exception ex)
            {
                LogKeyError(keyPath, ex);
                throw;
            }
        }

        private void LogKeyLoaded(string source, string path) => LogKeyLoadedAction(mLogger, source, path, null);
        private void LogKeyGenerated(string path) => LogKeyGeneratedAction(mLogger, path, null);
        private void LogKeyError(string path, Exception ex) => LogKeyErrorAction(mLogger, path, ex);

        private static readonly Action<ILogger, string, string, Exception?> LogKeyLoadedAction =
            LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(20, "KeyLoaded"), "API key loaded from {Source}: {Path}");

        private static readonly Action<ILogger, string, Exception?> LogKeyGeneratedAction =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(21, "KeyGenerated"), "New API key generated and saved to: {Path}");

        private static readonly Action<ILogger, string, Exception?> LogKeyErrorAction =
            LoggerMessage.Define<string>(LogLevel.Critical, new EventId(22, "KeyError"), "Failed to load or create API key at {Path}. Application cannot continue.");
    }
}