using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Helpres;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models.Common;
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

        public AudioController(string ip, int port, IPreferencesService prefs, HttpClient client)
        {
            mClient = client;
            mIpAddress = ip;
            mPrefs = prefs;
            mBaseUrl = UrlHelpers.BuildUrl(ip, port, "audio");
        }

        public async Task<IReadOnlyList<AudioDeviceDto>> GetDevicesAsync()
        {
            using var request = CreateRequest(HttpMethod.Get, $"{mBaseUrl}/devices");
            using var response = await mClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<List<AudioDeviceDto>>();
            return result ?? [];
        }

        public async Task<string?> GetDefaultDeviceIdAsync()
        {
            using var request = CreateRequest(HttpMethod.Get, $"{mBaseUrl}/default-device");
            using var response = await mClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return null;

            var dto = await response.Content.ReadFromJsonAsync<DefaultDeviceDto>();
            return dto?.Id;
        }

        public async Task SetDefaultAsync(string id)
        {
            using var request = CreateRequest(HttpMethod.Post, $"{mBaseUrl}/set?id={Uri.EscapeDataString(id)}");
            using var response = await mClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

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