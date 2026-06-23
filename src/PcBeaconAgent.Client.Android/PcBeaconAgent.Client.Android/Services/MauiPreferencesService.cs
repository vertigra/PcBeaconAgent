using Microsoft.Maui.Storage;
using PcBeaconAgent.Client.Core.Interfaces;
using System;
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
        private static readonly object IndexLock = new();

        public void Set<T>(string key, T value)
        {
            if (key.StartsWith(ApiKeyPrefix) && value is string apiKey)
            {
                // FIX: фактическая запись теперь вынесена в общий приватный метод
                // WriteApiKeyAsync — он же используется в SetSecureAsync ниже.
                // Здесь, в синхронном Set<T>, оборачиваем в try/catch и логируем —
                // вызывающая сторона не ждёт результат и не должна получить
                // необработанное исключение из фонового Task.Run.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await WriteApiKeyAsync(key, apiKey);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"SecureStorage.SetAsync failed for key '{key}': {ex.Message}");
                    }
                });
                return;
            }

            Preferences.Default.Set(key, JsonSerializer.Serialize(value));
        }

        /// <inheritdoc />
        public Task SetSecureAsync(string key, string value)
        {
            // FIX (новый метод): в отличие от Set<T>, здесь исключение НЕ
            // перехватывается — оно должно дойти до вызывающей стороны
            // (PairingViewModel), чтобы при сбое записи не отправлялось
            // уведомление об "успешном" паринге, которого на самом деле не было.
            if (!key.StartsWith(ApiKeyPrefix))
                throw new ArgumentException(
                    $"SetSecureAsync supports only '{ApiKeyPrefix}*' keys.", nameof(key));

            return WriteApiKeyAsync(key, value);
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
            lock (IndexLock)
            {
                return ReadIndex().Select(StripApiKeyPrefix).ToList();
            }
        }

        // FIX (новый общий метод): единственное место, где реально пишем в
        // SecureStorage и обновляем индекс. Используется и из fire-and-forget
        // Set<T>, и из awaited SetSecureAsync — без дублирования кода записи.
        private static async Task WriteApiKeyAsync(string key, string value)
        {
            await SecureStorage.Default.SetAsync(key, value);
            AddToIndex(key);
        }

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