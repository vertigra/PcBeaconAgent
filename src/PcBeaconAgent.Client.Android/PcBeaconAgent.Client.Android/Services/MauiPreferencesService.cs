using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Core.Interfaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.Services
{
    public class MauiPreferencesService : IPreferencesService
    {
        public void Set<T>(string key, T value)
        {
            if (key == "api_key" && value is string apiKey)
            {
                Task.Run(async () => await SecureStorage.Default.SetAsync(key, apiKey));
                return;
            }

            Preferences.Default.Set(key, JsonSerializer.Serialize(value));
        }

        public T? Get<T>(string key, T defaultValue)
        {
            if (key == "api_key")
            {
                var task = SecureStorage.Default.GetAsync(key);
                task.Wait();
                return (T)(task.Result ?? (object)(defaultValue ?? default!));
            }

            var json = Preferences.Default.Get(key, string.Empty);
            if (string.IsNullOrEmpty(json)) return defaultValue;

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