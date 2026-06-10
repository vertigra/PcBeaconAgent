using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class UdpBeaconScanner : IUdpBeaconScanner
{
    private readonly int mDiscoveryPort;
    public event Action<DiscoveredBeacon>? OnBeaconFound;

    public UdpBeaconScanner(int discoveryPort) => mDiscoveryPort = discoveryPort;

    public async Task ScanAsync(int timeoutMs = 2000)
    {
        using var client = new UdpClient();
        client.EnableBroadcast = true;

        byte[] request = { 0x01 };
        await client.SendAsync(request, request.Length, new IPEndPoint(IPAddress.Broadcast, mDiscoveryPort));

        using var cts = new CancellationTokenSource(timeoutMs);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cts.Token);

                if (result.Buffer.Length >= 3 && result.Buffer[0] == 0x02)
                {
                    var beacon = new DiscoveredBeacon
                    {
                        IpAddress = result.RemoteEndPoint.Address.ToString(),
                        Port = BitConverter.ToUInt16(result.Buffer, 1)
                    };

                    OnBeaconFound?.Invoke(beacon);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сканирования: {ex.Message}");
            }
        }
    }
}