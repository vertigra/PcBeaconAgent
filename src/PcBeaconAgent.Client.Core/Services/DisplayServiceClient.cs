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
    public class DisplayServiceClient : IDisplayServiceClient
    {
        private readonly HttpClient mClient;
        private readonly string mBaseUrl;
        private readonly string mIpAddress;
        private readonly IPreferencesService mPrefs;

        public DisplayServiceClient(string ip, int port, IPreferencesService prefs, HttpClient client)
        {
            mClient = client;
            mIpAddress = ip;
            mPrefs = prefs;
            mBaseUrl = UrlHelpers.BuildUrl(ip, port, "display");
        }

        public async Task<DisplayListResponseDto> GetDisplaysAsync()
        {
            using var request = CreateRequest(HttpMethod.Get, $"{mBaseUrl}/list");
            using var response = await mClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(ProjectJsonContext.Default.DisplayListResponseDto);
            return result ?? new DisplayListResponseDto([], "Unknown");
        }

        public async Task DisableAsync(string id)
        {
            using var request = CreateRequest(HttpMethod.Post, $"{mBaseUrl}/disable");
            request.Content = JsonContent.Create(new DisableRequestDto(id), ProjectJsonContext.Default.DisableRequestDto);

            using var response = await mClient.SendAsync(request);
            await EnsureSuccessWithServerMessageAsync(response);
        }

        public async Task RestoreAllAsync()
        {
            using var request = CreateRequest(HttpMethod.Post, $"{mBaseUrl}/restore");
            using var response = await mClient.SendAsync(request);
            await EnsureSuccessWithServerMessageAsync(response);
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);

            var apiKey = mPrefs.Get(StorageKeys.ApiKeyFor(mIpAddress), string.Empty);
            if (!string.IsNullOrEmpty(apiKey))
                request.Headers.Add("X-Api-Key", apiKey);

            return request;
        }

        /// <summary>
        /// Replaces EnsureSuccessStatusCode for write operations. The server
        /// returns a MessageDto in the body of 4xx responses (e.g. "Cannot
        /// disable the last active display..."), and EnsureSuccessStatusCode
        /// throws HttpRequestException without that body — the user would
        /// see a generic error instead of the server's explanation. This
        /// reads the MessageDto and surfaces its Message in the exception.
        /// </summary>
        private static async Task EnsureSuccessWithServerMessageAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            string? serverMessage = null;
            try
            {
                var dto = await response.Content.ReadFromJsonAsync(ProjectJsonContext.Default.MessageDto);
                serverMessage = dto?.Message;
            }
            catch
            {
                // Body was not a MessageDto (or empty) — fall back to the
                // status code.
            }

            throw new HttpRequestException(
                serverMessage ?? $"Server returned {(int)response.StatusCode} {response.StatusCode}.");
        }
    }
}