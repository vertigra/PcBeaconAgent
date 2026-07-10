using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Helpres;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Contracts;
using PcBeaconAgent.Contracts.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    /// <summary>
    /// HTTP client for the server's pairing endpoint. Stateless — takes the
    /// server IP and port per call because pairing happens before the device
    /// is added to <c>ManagedDevices</c>.
    /// </summary>
    public class PairingServiceClient : IPairingServiceClient
    {
        private readonly HttpClient mClient;

        public PairingServiceClient(HttpClient client)
        {
            mClient = client;
        }

        /// <inheritdoc />
        public async Task<PairResponseDto?> PairAsync(string ip, int port, string pin)
        {
            string url = UrlHelpers.BuildUrl(ip, port, "pair");

            using var response = await mClient.PostAsJsonAsync(
                url, new PairRequestDto(pin), ProjectJsonContext.Default.PairRequestDto);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync(ProjectJsonContext.Default.PairResponseDto);
            }

            // Surface the server's explanation (e.g. wrong PIN, pairing
            // inactive) via an exception so the ViewModel can show it.
            throw new PairingHttpException(
                await ReadServerMessageAsync(response),
                response.StatusCode);
        }

        /// <inheritdoc />
        public async Task<bool> RegeneratePinAsync(string ip, int port)
        {
            string url = $"{UrlHelpers.BuildUrl(ip, port, "pair")}/regenerate";

            using var response = await mClient.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }

        private static async Task<string> ReadServerMessageAsync(HttpResponseMessage response)
        {
            try
            {
                var dto = await response.Content.ReadFromJsonAsync(ProjectJsonContext.Default.MessageDto);
                return dto?.Message ?? $"Server returned {(int)response.StatusCode} {response.StatusCode}.";
            }
            catch
            {
                return $"Server returned {(int)response.StatusCode} {response.StatusCode}.";
            }
        }
    }
}
