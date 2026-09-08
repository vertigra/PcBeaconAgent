using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Helpres;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Contracts;
using PcBeaconAgent.Contracts.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class LauncherServiceClient : ILauncherServiceClient
    {
        private readonly HttpClient mClient;
        private readonly string mBaseUrl;
        private readonly string mIpAddress;
        private readonly IPreferencesService mPrefs;

        public LauncherServiceClient(string ip, int port, IPreferencesService prefs, HttpClient client)
        {
            mClient = client;
            mIpAddress = ip;
            mPrefs = prefs;
            mBaseUrl = UrlHelpers.BuildUrl(ip, port, "launchers");
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<LauncherDto>> GetLaunchersAsync()
        {
            using var request = CreateRequest(HttpMethod.Get, mBaseUrl);
            using var response = await mClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(
                ProjectJsonContext.Default.ListLauncherDto);
            return result ?? [];
        }

        /// <inheritdoc />
        public async Task<LaunchResponseDto> LaunchAsync(string id)
        {
            using var request = CreateRequest(HttpMethod.Post, $"{mBaseUrl}/{id}/launch");
            using var response = await mClient.SendAsync(request);

            var dto = await response.Content.ReadFromJsonAsync(
                ProjectJsonContext.Default.LaunchResponseDto);

            if (dto == null)
            {
                response.EnsureSuccessStatusCode();
                return new LaunchResponseDto(false, "Server returned an unexpected response.", 0);
            }

            return dto;
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
