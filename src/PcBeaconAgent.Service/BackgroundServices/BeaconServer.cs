using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Service.BackgroundServices
{
    public class BeaconServer : IBeaconServer 
    {
        private readonly BeaconServerOptions mBeaconServerOptions;
        private readonly WebApiOptions mWebApiOptions;
        private readonly ILogger<BeaconServer> mLogger;

        public event Action<IPEndPoint, int>? OnResponseSent;

        private const byte ping = 0x01;
        private const byte pong = 0x02;

        public BeaconServer(BeaconServerOptions beaconServerOptions, WebApiOptions webApiOptions, ILogger<BeaconServer> logger)
        {
            mBeaconServerOptions = beaconServerOptions;
            mWebApiOptions = webApiOptions;
            mLogger = logger;
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            using var udpServer = new UdpClient();
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpServer.Client.Bind(new IPEndPoint(IPAddress.Parse(mBeaconServerOptions.BindingIp), mBeaconServerOptions.DiscoveryPort));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await udpServer.ReceiveAsync(stoppingToken);

                    if (result.Buffer.Length > 0 && result.Buffer[0] == ping)
                    {
                        byte[] portBytes = BitConverter.GetBytes((ushort)mWebApiOptions.ApiPort);
                        byte[] response = [pong, portBytes[0], portBytes[1]];

                        await udpServer.SendAsync(response, response.Length, result.RemoteEndPoint);

                        mLogger.LogInformation("Sent API port {Port} to {EndPoint}", mWebApiOptions.ApiPort, result.RemoteEndPoint);

                        OnResponseSent?.Invoke(result.RemoteEndPoint, mWebApiOptions.ApiPort);
                    }
                }
                catch (OperationCanceledException)
                {
                    mLogger.LogInformation("UDP Beacon Server is shutting down...");
                }
                catch (Exception ex)
                {
                    mLogger.LogError(ex, "An error occurred while listening for UDP broadcasts");
                }
            }
        }
    }
}