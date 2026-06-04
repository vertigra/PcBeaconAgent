using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Configuration;
using System.Net.Sockets;
using System.Text;

namespace PcBeaconAgent.Services;

public class UdpDiscoveryBackgroundService(AppSettings mSettings, ILogger<UdpDiscoveryBackgroundService> mLogger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int udpPort = mSettings.Server.DiscoveryPort;

        if (mLogger.IsEnabled(LogLevel.Information))
            mLogger.LogInformation("UDP Discovery background service started on port {Port}", udpPort);

        using var udpListener = new UdpClient();
        udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpListener.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, udpPort));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpListener.ReceiveAsync(stoppingToken);
                string message = Encoding.UTF8.GetString(result.Buffer);

                if (message == "PING_PC_BEACON_AGENT")
                {
                    if (mLogger.IsEnabled(LogLevel.Information))
                        mLogger.LogInformation("Discovery request received from {Client}", result.RemoteEndPoint);

                    byte[] responseData = Encoding.UTF8.GetBytes($"PONG_PC_BEACON_AGENT:{mSettings.Server.Port}");
                    await udpListener.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                mLogger.LogError(ex, "An error occurred while processing the UDP request");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}