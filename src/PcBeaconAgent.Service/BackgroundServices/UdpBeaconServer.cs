using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Service.Configuration;
using PcBeaconAgent.Service.Interfaces;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class UdpBeaconServer : BackgroundService
{
    private readonly AppSettings mSettings;
    private readonly IBeaconAnnouncementService mBeaconService;
    private readonly ILogger<UdpBeaconServer> mLogger;

    public event Action<IPEndPoint, int>? OnResponseSent;

    private const byte ping = 0x01;
    private const byte pong = 0x02;

    public UdpBeaconServer(AppSettings settings, IBeaconAnnouncementService beaconService, ILogger<UdpBeaconServer> logger)
    {
        mSettings = settings;
        mBeaconService = beaconService;
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
                    byte[] keyBytes = Encoding.UTF8.GetBytes(mBeaconService.ApiKey);

                    byte[] response = new byte[3 + keyBytes.Length];
                    response[0] = pong;
                    response[1] = portBytes[0];
                    response[2] = portBytes[1];
                    Array.Copy(keyBytes, 0, response, 3, keyBytes.Length);

                    await udpServer.SendAsync(response, response.Length, result.RemoteEndPoint);

                    mLogger.LogInformation("Sent API port and Key to {EndPoint}", result.RemoteEndPoint);
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