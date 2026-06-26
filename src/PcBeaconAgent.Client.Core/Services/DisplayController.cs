using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Helpres;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Service.JsonContext;
using PcBeaconAgent.Service.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class DisplayController : IDisplayController
    {
        private readonly HttpClient mClient;
        private readonly string mBaseUrl;
        private readonly string mIpAddress;
        private readonly IPreferencesService mPrefs;

        public DisplayController(string ip, int port, IPreferencesService prefs, HttpClient client)
        {
            mClient = client;
            mIpAddress = ip;
            mPrefs = prefs;
            mBaseUrl = UrlHelpers.BuildUrl(ip, port, "monitor");
        }

        public async Task<IReadOnlyList<DisplayDeviceDto>> GetDisplaysAsync()
        {
            using var request = CreateRequest(HttpMethod.Get, $"{mBaseUrl}/list");
            using var response = await mClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(ProjectJsonContext.Default.ListDisplayDeviceDto);
            return result ?? [];
        }

        public async Task DisableAsync(string id)
        {
            using var request = CreateRequest(HttpMethod.Post, $"{mBaseUrl}/disable");            
            request.Content = JsonContent.Create(new DisableRequestDto(id), ProjectJsonContext.Default.DisableRequestDto);

            using var response = await mClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task RestoreAllAsync()
        {
            using var request = CreateRequest(HttpMethod.Post, $"{mBaseUrl}/restore");
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