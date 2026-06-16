using PcBeaconAgent.Client.Core.Interfaces;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class MonitorController : IMonitorController
    {
        private readonly HttpClient mClient;
        public MonitorController(string ip, int port, HttpClient client)
        {
            mClient = client;
            mClient.BaseAddress = new Uri($"http://{ip}:{port}/api/monitor/");
        }
        public async Task TogglePowerAsync(bool on) => await mClient.PostAsync($"power?on={on}", null);
    }
}
