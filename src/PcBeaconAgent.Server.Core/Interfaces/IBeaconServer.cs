using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Server.Core.Interfaces
{
    public interface IBeaconServer
    {
        event Action<IPEndPoint, int>? OnResponseSent;
        Task StartAsync(CancellationToken stoppingToken);
    }
}
