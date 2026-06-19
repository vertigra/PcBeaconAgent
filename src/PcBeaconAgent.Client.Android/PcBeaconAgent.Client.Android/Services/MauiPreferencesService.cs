using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.Services
{
    public class MauiPreferencesService : IPreferencesService
    {
        public void Set<T>(string key, T value)
        {
            if (key == StorageKeys.ApiKey && value is string apiKey)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SecureStorage.Default.SetAsync(key, apiKey);
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"SecureStorage.SetAsync failed for key '{key}': {ex.Message}");
                    }
                });
                return;
            }

            Preferences.Default.Set(key, JsonSerializer.Serialize(value));
        }

        public T? Get<T>(string key, T defaultValue)
        {
            if (key == StorageKeys.ApiKey)
            {
                try
                {
                    var result = Task.Run(async () =>
                        await SecureStorage.Default.GetAsync(key))
                        .GetAwaiter().GetResult();

                    if (result is T typedResult)
                        return typedResult;

                    return defaultValue;
                }
                catch
                {
                    return defaultValue;
                }
            }

            var json = Preferences.Default.Get(key, string.Empty);
            if (string.IsNullOrEmpty(json))
                return defaultValue;

            try
            {
                return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
            }
            catch (JsonException)
            {
                return defaultValue;
            }
        }
    }
}