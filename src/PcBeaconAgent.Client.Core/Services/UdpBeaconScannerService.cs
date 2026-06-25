namespace PcBeaconAgent.Client.Core.Services;

using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models.Common;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class UdpBeaconScannerService : IUdpBeaconScannerService
{
    public event Action<DiscoveredBeacon>? OnBeaconFound;

    private readonly int mDiscoveryPort;
    private readonly ILogger<UdpBeaconScannerService> mLogger;

    public UdpBeaconScannerService(IPreferencesService preferences, ILogger<UdpBeaconScannerService> logger)
    {
        mDiscoveryPort = preferences.Get(StorageKeys.DiscoveryPort, 8888);
        mLogger = logger;
    }

    public async Task ScanAsync(int timeoutMs = 2000)
    {
        using var client = new UdpClient();
        client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        client.EnableBroadcast = true;

        byte[] request = [0x01];
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
                mLogger.LogDebug("Discovery scan timed out.");
                break;
            }
            catch (Exception ex)
            {
                mLogger.LogError(ex, "Error during beacon discovery");
            }
        }
    }
}