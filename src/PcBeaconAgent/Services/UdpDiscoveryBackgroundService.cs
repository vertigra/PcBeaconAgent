using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Configuration;
using System.Net.Sockets;
using System.Text;

namespace PcBeaconAgent.Services
{
    public class UdpDiscoveryBackgroundService(AppSettings mSettings, ILogger<UdpDiscoveryBackgroundService> mLogger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int udpPort = mSettings.Server.DiscoveryPort;

            if (mLogger.IsEnabled(LogLevel.Information))
                mLogger.LogInformation("Фоновая служба UDP Discovery запущена на порту {Port}", udpPort);

            using var udpListener = new UdpClient(udpPort);
            udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await udpListener.ReceiveAsync(stoppingToken);
                    string message = Encoding.UTF8.GetString(result.Buffer);

                    if (message == "PING_PC_CONTROLLER")
                    {
                        if (mLogger.IsEnabled(LogLevel.Information))
                            mLogger.LogInformation("Получен поисковый запрос от {Client}", result.RemoteEndPoint);

                        byte[] responseData = Encoding.UTF8.GetBytes($"PONG_PC_CONTROLLER:{mSettings.Server.Port}");
                        await udpListener.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    mLogger.LogError(ex, "Ошибка при обработке UDP-запроса");
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }
    }
}