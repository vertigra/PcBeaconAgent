using PcBeaconAgent.Client.Core.Interfaces;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class MonitorController : IMonitorController
    {
        private readonly HttpClient _client;
        public MonitorController(string ip, int port, HttpClient client)
        {
            _client = client;
            _client.BaseAddress = new Uri($"http://{ip}:{port}/api/monitor/");
        }
        public async Task TogglePowerAsync(bool on) => await _client.PostAsync($"power?on={on}", null);
    }
}
