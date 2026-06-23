using PcBeaconAgent.Client.Core.Helpres;
using PcBeaconAgent.Client.Core.Interfaces;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class AudioController : IAudioController
    {
        private readonly HttpClient mClient;
        private readonly string mBaseUrl;

        public AudioController(string ip, int port, HttpClient client)
        {
            mClient = client;
            mBaseUrl = UrlHelpers.BuildUrl(ip, port, "audio");
        }

        public async Task SetDefaultAsync(string id)  => await mClient.PostAsync($"{mBaseUrl}/set?id={id}", null);
    }
}