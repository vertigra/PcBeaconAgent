using Microsoft.Extensions.Hosting;
using PcBeaconAgent.Server.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Server.Core.BackgroundServices
{
    public class BeaconBackgroundService(IBeaconServer mServer) : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => mServer.StartAsync(stoppingToken);
    }
}
