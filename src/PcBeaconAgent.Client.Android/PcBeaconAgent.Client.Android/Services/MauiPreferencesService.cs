using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Core.Interfaces;
using System.Text.Json;

namespace PcBeaconAgent.Client.Android.Services
{
    public class MauiPreferencesService : IPreferencesService
    {
        public void Set<T>(string key, T value) => Preferences.Default.Set(key, JsonSerializer.Serialize(value));
        public T? Get<T>(string key, T defaultValue)
        {
            var json = Preferences.Default.Get(key, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return defaultValue;
            }

            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
