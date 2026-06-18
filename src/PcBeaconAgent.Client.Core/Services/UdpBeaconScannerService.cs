namespace PcBeaconAgent.Client.Core.Services;

using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class UdpBeaconScannerService : IUdpBeaconScannerService
{
    private readonly int mDiscoveryPort;

    public event Action<DiscoveredBeacon>? OnBeaconFound;

    // FIX: убрана зависимость от IPreferencesService — сервис сканирования больше
    // не читает и не сохраняет ключ из UDP-ответа. Ключ теперь конфигурируется
    // вручную пользователем через SettingsPage. Ответственность за хранение ключа
    // полностью перенесена на SettingsViewModel + MauiPreferencesService.
    public UdpBeaconScannerService(IPreferencesService preferences)
    {
        mDiscoveryPort = preferences.Get(StorageKeys.DiscoveryPort, 8888);
    }

    public async Task ScanAsync(int timeoutMs = 2000)
    {
        using var client = new UdpClient();
        client.EnableBroadcast = true;

        byte[] request = { 0x01 };
        await client.SendAsync(request, request.Length,
            new IPEndPoint(IPAddress.Broadcast, mDiscoveryPort));

        using var cts = new CancellationTokenSource(timeoutMs);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(cts.Token);

                // FIX: разбираем только pong (0x02) + 2 байта порта.
                // Ранее здесь ещё читались байты ключа (buffer[3..]) и записывались
                // в preferences — это и создавало дыру в безопасности.
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
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scan error: {ex.Message}");
            }
        }
    }
}