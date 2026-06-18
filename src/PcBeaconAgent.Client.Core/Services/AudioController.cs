using PcBeaconAgent.Client.Core.Interfaces;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class AudioController : IAudioController
    {
        private readonly HttpClient mClient;
        public AudioController(string ip, int port, HttpClient client)
        {
            mClient = client;
            mClient.BaseAddress = new Uri($"http://{ip}:{port}/api/audio/");
        }
        public async Task SetDefaultAsync(string id) => await mClient.PostAsync($"set?id={id}", null);
    }
}
