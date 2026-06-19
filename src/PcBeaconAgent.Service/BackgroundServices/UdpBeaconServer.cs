using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Service.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class UdpBeaconServer : BackgroundService
{
    private readonly AppSettings mSettings;
    private readonly ILogger<UdpBeaconServer> mLogger;

    public event Action<IPEndPoint, int>? OnResponseSent;

    private const byte ping = 0x01;
    private const byte pong = 0x02;

    public UdpBeaconServer(AppSettings settings, ILogger<UdpBeaconServer> logger)
    {
        mSettings = settings;
        mLogger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udpServer = new UdpClient();
        udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, mSettings.Server.DiscoveryPort));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpServer.ReceiveAsync(stoppingToken);

                if (result.Buffer.Length > 0 && result.Buffer[0] == ping)
                {
                    byte[] portBytes = BitConverter.GetBytes((ushort)mSettings.Server.ApiPort);
                    byte[] response = [pong, portBytes[0], portBytes[1]];

                    await udpServer.SendAsync(response, response.Length, result.RemoteEndPoint);

                    mLogger.LogInformation("Sent API port {Port} to {EndPoint}",
                        mSettings.Server.ApiPort, result.RemoteEndPoint);

                    OnResponseSent?.Invoke(result.RemoteEndPoint, mSettings.Server.ApiPort);
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