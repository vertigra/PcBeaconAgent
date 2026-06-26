using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Service.BackgroundServices
{
    public class BeaconBackgroundService : BackgroundService
    {
        private readonly IBeaconServer mServer;

        public BeaconBackgroundService(IBeaconServer server) => mServer = server;

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => mServer.StartAsync(stoppingToken);
    }
}
