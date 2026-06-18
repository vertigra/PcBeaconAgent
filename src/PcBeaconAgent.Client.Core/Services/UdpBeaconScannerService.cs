namespace PcBeaconAgent.Client.Core.Services;

using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class UdpBeaconScannerService : IUdpBeaconScannerService
{
    private readonly int mDiscoveryPort;
    private readonly IPreferencesService mPreferences; 
    public event Action<DiscoveredBeacon>? OnBeaconFound;

    public UdpBeaconScannerService(IPreferencesService preferences)
    {
        mPreferences = preferences;
        mDiscoveryPort = mPreferences.Get(StorageKeys.DiscoveryPort, 8888); 
    }

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
                    ushort port = BitConverter.ToUInt16(result.Buffer, 1);
                    string apiKey = Encoding.UTF8.GetString(result.Buffer, 3, result.Buffer.Length - 3);

                    mPreferences.Set(StorageKeys.ApiKey, apiKey);

                    var beacon = new DiscoveredBeacon
                    {
                        IpAddress = result.RemoteEndPoint.Address.ToString(),
                        Port = port
                    };
                    OnBeaconFound?.Invoke(beacon);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Scan error: {ex.Message}"); }
        }
    }
}