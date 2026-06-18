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


            try
            {
                // FIX: защита от повреждённого/несовместимого JSON (например, после
                // обновления приложения изменилась форма модели) — раньше необработанное
                // исключение тут было фатальным, т.к. MainViewModel.LoadDevice() вызывает
                // это синхронно из конструктора.
                return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
            }
            catch (JsonException)
            {
                return defaultValue;
            }
        }
    }
}
