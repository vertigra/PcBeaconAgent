using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.Services
{
    public class MauiPreferencesService(ILogger<MauiPreferencesService> mLogger) : IPreferencesService
    {
        private const string ApiKeyPrefix = "api_key";
        private const string ApiKeyIndexStorageKey = "api_key_index";
        private static readonly Lock IndexLock = new();

        public void Set<T>(string key, T value)
        {
            if (key.StartsWith(ApiKeyPrefix) && value is string apiKey)
            {
                _ = Task.Run(async () => {
                    try 
                    { 
                        await WriteApiKeyAsync(key, apiKey); 
                    }
                    catch (Exception ex) 
                    { 
                        LogSetError(key, ex); 
                    }
                });
                return;
            }
            Preferences.Default.Set(key, JsonSerializer.Serialize(value));
        }

        public Task SetSecureAsync(string key, string value) => WriteApiKeyAsync(key, value);

        public T? Get<T>(string key, T defaultValue)
        {
            if (key.StartsWith(ApiKeyPrefix))
            {
                try
                {
                    var val = Task.Run(async () => await SecureStorage.Default.GetAsync(key)).GetAwaiter().GetResult();
                    return (val is T typed) ? typed : defaultValue;
                }
                catch (Exception ex) 
                { 
                    LogGetError(key, ex); 
                    return defaultValue; 
                }
            }
            var json = Preferences.Default.Get(key, string.Empty);
            return string.IsNullOrEmpty(json) ? defaultValue : (JsonSerializer.Deserialize<T>(json) ?? defaultValue);
        }

        public void Remove(string key)
        {
            if (key.StartsWith(ApiKeyPrefix))
            {
                SecureStorage.Default.Remove(key);
                RemoveFromIndex(key, mLogger); 
            }
            else
                Preferences.Default.Remove(key);
        }

        private async Task WriteApiKeyAsync(string key, string value)
        {
            await SecureStorage.Default.SetAsync(key, value);
            AddToIndex(key, mLogger);
            LogKeyWrite(key);
        }

        private static void AddToIndex(string fullKey, ILogger logger)
        {
            lock (IndexLock)
            {
                var index = ReadIndex();
                if (!index.Contains(fullKey))
                {
                    index.Add(fullKey);
                    WriteIndex(index);
                }
            }
        }

        private static void RemoveFromIndex(string fullKey, ILogger logger)
        {
            lock (IndexLock)
            {
                var index = ReadIndex();
                if (index.Remove(fullKey))
                    WriteIndex(index);
            }
        }

        private static List<string> ReadIndex()
        {
            var json = Preferences.Default.Get(ApiKeyIndexStorageKey, string.Empty);
            return string.IsNullOrEmpty(json) ? [] : (JsonSerializer.Deserialize<List<string>>(json) ?? []);
        }

        private static void WriteIndex(List<string> index) => Preferences.Default.Set(ApiKeyIndexStorageKey, JsonSerializer.Serialize(index));

        public IReadOnlyList<string> GetStoredApiKeyIdentifiers() => [.. ReadIndex().Select(k => k == ApiKeyPrefix ? "global" : k[(ApiKeyPrefix.Length + 1)..])];

        private static readonly Action<ILogger, string, Exception?> LogSetErrorAction =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(30, "SetError"), "SecureStorage.SetAsync failed: {Key}");

        private static readonly Action<ILogger, string, Exception?> LogGetErrorAction =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(31, "GetError"), "Failed to get secure key: {Key}");

        private static readonly Action<ILogger, string, Exception?> LogKeyWriteAction =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(33, "KeyWrite"), "Wrote secure key: {Key}");

        private void LogSetError(string key, Exception ex) => LogSetErrorAction(mLogger, key, ex);
        private void LogGetError(string key, Exception ex) => LogGetErrorAction(mLogger, key, ex);
        private void LogKeyWrite(string key) => LogKeyWriteAction(mLogger, key, null);
    }
}