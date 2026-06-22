using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Android.Services
{
    public class MauiPreferencesService : IPreferencesService
    {
        private const string ApiKeyPrefix = "api_key";
        private const string ApiKeyIndexStorageKey = "api_key_index";

        // FIX (новое): блокировка вокруг операций "прочитать индекс → изменить →
        // записать обратно". Без неё два почти одновременных Set/Remove (например,
        // быстрый паринг двух устройств подряд) могут оба прочитать один и тот же
        // снимок индекса, и тот, кто запишет последним, затрёт изменение другого —
        // одна из записей в индексе потеряется, хотя сам ключ в SecureStorage
        // останется на месте (несоответствие между индексом и реальным хранилищем).
        //
        // Обычный `lock` (а не SemaphoreSlim/асинхронная блокировка) осознанно —
        // под блокировкой нет ни одного await: и Preferences.Default.Get/Set, и
        // сериализация JSON выполняются синхронно и быстро, поэтому удерживание
        // потока на это время безопасно и не создаёт риска deadlock'а, который
        // возник бы при попытке await'ить что-то внутри lock-блока.
        private static readonly object IndexLock = new();

        public void Set<T>(string key, T value)
        {
            if (key.StartsWith(ApiKeyPrefix) && value is string apiKey)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SecureStorage.Default.SetAsync(key, apiKey);
                        AddToIndex(key);
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
            if (key.StartsWith(ApiKeyPrefix))
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

        public void Remove(string key)
        {
            if (key.StartsWith(ApiKeyPrefix))
            {
                SecureStorage.Default.Remove(key);
                RemoveFromIndex(key);
                return;
            }

            Preferences.Default.Remove(key);
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetStoredApiKeyIdentifiers()
        {
            // FIX: чтение индекса тоже под блокировкой — без этого чтение могло
            // бы застать индекс в промежуточном состоянии относительно
            // одновременно идущей записи (хотя сам Preferences.Default.Get
            // атомарен на уровне одного вызова, нам нужна согласованность с
            // именно нашей операцией добавления/удаления, а не просто отсутствие
            // повреждённого JSON).
            lock (IndexLock)
            {
                return ReadIndex().Select(StripApiKeyPrefix).ToList();
            }
        }

        // --- ведение индекса ---

        private static void AddToIndex(string fullKey)
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

        private static void RemoveFromIndex(string fullKey)
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
            if (string.IsNullOrEmpty(json))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private static void WriteIndex(List<string> index)
            => Preferences.Default.Set(ApiKeyIndexStorageKey, JsonSerializer.Serialize(index));

        private static string StripApiKeyPrefix(string fullKey) =>
            fullKey == ApiKeyPrefix
                ? "global"
                : fullKey[(ApiKeyPrefix.Length + 1)..];
    }
}