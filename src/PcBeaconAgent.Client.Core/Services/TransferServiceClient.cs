using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Helpres;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Contracts;
using PcBeaconAgent.Contracts.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class TransferServiceClient : ITransferServiceClient
    {
        private readonly HttpClient mClient;
        private readonly string mBaseUrl;
        private readonly string mIpAddress;
        private readonly IPreferencesService mPrefs;

        public TransferServiceClient(string ip, int port, IPreferencesService prefs, HttpClient client)
        {
            mClient = client;
            mIpAddress = ip;
            mPrefs = prefs;
            mBaseUrl = UrlHelpers.BuildUrl(ip, port, "transfer");
        }

        /// <inheritdoc />
        public async Task<TextTransferResponseDto> SendTextAsync(string text)
        {
            using var request = CreateRequest(HttpMethod.Post, $"{mBaseUrl}/text");
            request.Content = JsonContent.Create(
                new TextTransferRequestDto(text),
                ProjectJsonContext.Default.TextTransferRequestDto);

            using var response = await mClient.SendAsync(request);

            // The server returns TextTransferResponseDto on both 200
            // (accepted) and 400 (rejected — empty payload, too large).
            // Read the DTO in both cases so the caller can show the
            // server's message instead of a generic HTTP error.
            var dto = await response.Content.ReadFromJsonAsync(
                ProjectJsonContext.Default.TextTransferResponseDto);

            // If the body was not a TextTransferResponseDto (unexpected),
            // fall back to EnsureSuccessStatusCode's standard behaviour.
            if (dto == null)
            {
                response.EnsureSuccessStatusCode();
                return new TextTransferResponseDto(false, "Server returned an unexpected response.");
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
