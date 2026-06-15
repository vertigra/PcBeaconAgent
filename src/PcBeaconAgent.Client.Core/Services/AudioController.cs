using PcBeaconAgent.Client.Core.Interfaces;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class AudioController : IAudioController
    {
        private readonly HttpClient _client;
        public AudioController(string ip, int port, HttpClient client)
        {
            _client = client;
            _client.BaseAddress = new Uri($"http://{ip}:{port}/api/audio/");
        }
        public async Task SetDefaultAsync(string id) => await _client.PostAsync($"set?id={id}", null);
    }
}
