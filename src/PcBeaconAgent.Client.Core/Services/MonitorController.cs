using PcBeaconAgent.Client.Core.Helpres;
using PcBeaconAgent.Client.Core.Interfaces;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    public class MonitorController : IDisplayController
    {
        private readonly HttpClient mClient;
        private readonly string mBaseUrl;

        public MonitorController(string ip, int port, HttpClient client)
        {
            mClient = client;
            mBaseUrl = UrlHelpers.BuildUrl(ip, port, "monitor");
        }

        public async Task TogglePowerAsync(bool on)
            => await mClient.PostAsync($"{mBaseUrl}/power?on={on}", null);
    }
}