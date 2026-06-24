using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Helpres;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class AudioController : IAudioController
    {
        private readonly HttpClient mClient;
        private readonly string mBaseUrl;
        private readonly string mIpAddress;
        private readonly IPreferencesService mPrefs;

        // FIX: было ip/port/HttpClient без какой-либо авторизации — ни один вызов
        // к /api/audio/* не смог бы пройти RequireApiKey-проверку на сервере.
        // Теперь IPreferencesService читается на каждый запрос отдельно (см.
        // CreateRequest), а не один раз в конструкторе — это сделано намеренно,
        // чтобы после повторного паринга (новый PIN → новый ключ) уже созданный
        // экземпляр сразу подхватывал новое значение, а не продолжал слать старый
        // ключ из DefaultRequestHeaders до перезапуска приложения.
        public AudioController(string ip, int port, IPreferencesService prefs, HttpClient client)
        {
            mClient = client;
            mIpAddress = ip;
            mPrefs = prefs;
            mBaseUrl = UrlHelpers.BuildUrl(ip, port, "audio");
        }

        public async Task<IReadOnlyList<AudioDeviceInfo>> GetDevicesAsync()
        {
            using var request = CreateRequest(HttpMethod.Get, $"{mBaseUrl}/devices");
            using var response = await mClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<List<AudioDeviceInfo>>();
            return result ?? [];
        }

        public async Task<string?> GetDefaultDeviceIdAsync()
        {
            using var request = CreateRequest(HttpMethod.Get, $"{mBaseUrl}/default-device");
            using var response = await mClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<DefaultDeviceResponse>();
            return dto?.Id;
        }

        public async Task SetDefaultAsync(string id)
        {
            using var request = CreateRequest(HttpMethod.Post, $"{mBaseUrl}/set?id={Uri.EscapeDataString(id)}");
            using var response = await mClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        // FIX (новый метод): единая точка, где собирается запрос с актуальным
        // на этот момент ключом. StorageKeys.ApiKeyFor(ip) — тот же индексируемый
        // per-device ключ, что используется и для SignalR-подключений.
        private HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);

            var apiKey = mPrefs.Get(StorageKeys.ApiKeyFor(mIpAddress), string.Empty);
            if (!string.IsNullOrEmpty(apiKey))
                request.Headers.Add("X-Api-Key", apiKey);

            return request;
        }
    }
}