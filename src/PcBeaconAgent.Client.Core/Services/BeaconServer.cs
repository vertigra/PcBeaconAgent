using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Configuration;
using PcBeaconAgent.Client.Core.Interfaces;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services
{
    internal class BeaconServer : IBeaconServer
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

                        LogPortSent(mWebApiOptions.ApiPort, result.RemoteEndPoint);

                        OnResponseSent?.Invoke(result.RemoteEndPoint, mWebApiOptions.ApiPort);
                    }
                }
                catch (OperationCanceledException)
                {
                    LogShuttingDown();
                }
                catch (Exception ex)
                {
                    LogListenError(ex);
                }
            }
        }

        #region Structured logging definitions (allocation-free)

        private static readonly Action<ILogger, int, IPEndPoint, Exception?> LogPortSentAction =
            LoggerMessage.Define<int, IPEndPoint>(
                LogLevel.Information,
                new EventId(40, "PortSent"),
                "Sent API port {Port} to {EndPoint}");

        private static readonly Action<ILogger, Exception?> LogShuttingDownAction =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(41, "ShuttingDown"),
                "UDP Beacon Server is shutting down...");

        private static readonly Action<ILogger, Exception?> LogListenErrorAction =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(42, "ListenError"),
                "An error occurred while listening for UDP broadcasts");

        private void LogPortSent(int port, IPEndPoint endPoint) => LogPortSentAction(mLogger, port, endPoint, null);
        private void LogShuttingDown() => LogShuttingDownAction(mLogger, null);
        private void LogListenError(Exception ex) => LogListenErrorAction(mLogger, ex);

        #endregion
    }
}