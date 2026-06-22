using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Constants;
using PcBeaconAgent.Client.Core.Exceptions;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PcBeaconAgent.Client.Core.Services;

public class SignalService(ILogger<SignalService> mLogger, IPreferencesService mPrefs) : ISignalService
{
    public event Action<string, BeaconDevice>? DeviceDetailsReceived;
    public event Action<string, bool>? DeviceStatusChanged;

    private readonly Dictionary<string, HubConnection> mConnections = [];

    /// <inheritdoc />
    public async Task ConnectToBeaconHubAsync(BeaconDevice beaconDevice)
    {
        string hubUrl = $"http://{beaconDevice.IpAddress}:{beaconDevice.ApiPort}/hubs/beacon";
        await ConnectAsync(beaconDevice.IpAddress, hubUrl);
    }

    /// <inheritdoc />
    public async Task DisconnectBeaconHubAsync(string ipAddress)
    {
        if (mConnections.Remove(ipAddress, out var connection))
        {
            await connection.StopAsync();
            await connection.DisposeAsync();
            LogConnectionStopped(ipAddress);
        }
    }

    /// <inheritdoc />
    public async Task DisconnectBeaconHubAsync(BeaconDevice beaconDevice)
    {
        await DisconnectBeaconHubAsync(beaconDevice.IpAddress);
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(string ipAddress, string command, object data)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection) && connection.State == HubConnectionState.Connected)
            await connection.SendAsync(command, data);
    }

    /// <inheritdoc />
    public async Task SendCommandAsync(string ipAddress, string command)
    {
        if (mConnections.TryGetValue(ipAddress, out var connection) && connection.State == HubConnectionState.Connected)
            await connection.SendAsync(command);
    }

    /// <inheritdoc />
    public async Task<BeaconDevice> ReceiveDeviceDetailsAndCloseAsync(BeaconDevice beaconDevice, CancellationToken ct = default)
    {
        string hubUrl = $"http://{beaconDevice.IpAddress}:{beaconDevice.ApiPort}/hubs/beacon";

        // FIX: раньше метод просто делал ConnectAsync + SendCommandAsync и завершался —
        // SendCommandAsync дожидается лишь отправки сообщения, а не прихода ответа
        // ReceiveDeviceDetails. Теперь подписываемся на общее событие DeviceDetailsReceived
        // временно, только на время этого вызова, и фильтруем по IP — это даёт детерминированную
        // точку синхронизации "данные точно получены", без блокирующих ожиданий.
        var tcs = new TaskCompletionSource<BeaconDevice>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnDetailsReceived(string ip, BeaconDevice data)
        {
            if (ip == beaconDevice.IpAddress)
                tcs.TrySetResult(data);
        }

        DeviceDetailsReceived += OnDetailsReceived;

        try
        {
            await ConnectAsync(beaconDevice.IpAddress, hubUrl, ct);
            await SendCommandAsync(beaconDevice.IpAddress, "ReceiveDeviceDetailsAndClose");

            // FIX: явный таймаут — без него зависший/неотвечающий сервер (например,
            // PIN ввели верно, но устройство сразу ушло из сети) подвесил бы Remember()
            // навечно, так как await tcs.Task ничем не ограничен по времени.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(DeviceDetailsTimeout);

            using var registration = timeoutCts.Token.Register(() =>
                tcs.TrySetException(new TimeoutException(
                    $"Timed out waiting for device details from {beaconDevice.IpAddress}.")));

            return await tcs.Task;
        }
        finally
        {
            // FIX: обязательная отписка — иначе при повторных вызовах (например,
            // пользователь несколько раз жмёт "Remember" на разные устройства)
            // накопились бы "висящие" обработчики, каждый сравнивающий чужой IP.
            DeviceDetailsReceived -= OnDetailsReceived;
        }
    }

    // Сколько ждать ответа сервера на запрос деталей устройства, прежде чем считать
    // это сетевой ошибкой/таймаутом. 5 секунд — достаточно для локальной сети,
    // но не настолько долго, чтобы UI казался "подвисшим" при нажатии Remember.
    private static readonly TimeSpan DeviceDetailsTimeout = TimeSpan.FromSeconds(5);
    /// <inheritdoc />
    public async Task ForgetAsync(string ipAddress)
    {
        await DisconnectBeaconHubAsync(ipAddress);
        mPrefs.Remove(StorageKeys.ApiKeyFor(ipAddress));
        LogKeyForgotten(ipAddress);
    }

    /// <summary>
    /// Establishes a new connection to a hub at the specified address.
    /// Supports automatic reconnection.
    /// </summary>
    /// <param name="ipAddress">The IP address of the target device (used as a key).</param>
    /// <param name="hubUrl">The full URL of the SignalR hub.</param>
    /// <param name="ct">Cancellation token for the connection process.</param>

    private async Task ConnectAsync(string ipAddress, string hubUrl, CancellationToken ct = default)
    {
        if (mConnections.TryGetValue(ipAddress, out var conn))
        {
            if (conn.State is HubConnectionState.Connected or HubConnectionState.Connecting)
                return;
            await DisconnectBeaconHubAsync(ipAddress);
        }

        var connection = CreateConnection(ipAddress, hubUrl);
        mConnections[ipAddress] = connection;

        try
        {
            await connection.StartAsync(ct);
            LogConnectionStarted(ipAddress);
            DeviceStatusChanged?.Invoke(ipAddress, true);
        }
        catch (Exception ex)
        {
            LogConnectionError(ipAddress, ex);
            DeviceStatusChanged?.Invoke(ipAddress, false);
        }
    }

    private HubConnection CreateConnection(string ipAddress, string hubUrl)
    {
        var apiKey = ResolveApiKey(ipAddress);

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new NotPairedException();
        }

        var connection = new HubConnectionBuilder()
             .WithUrl(hubUrl, options =>
             {
                 if (!string.IsNullOrEmpty(apiKey))
                 {
                     options.Headers["X-Api-Key"] = apiKey;
                 }
             })
            .WithAutomaticReconnect()
            .Build();

        connection.Closed += (_) =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, false);
            return Task.CompletedTask;
        };

        connection.Reconnecting += (_) =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, false);
            return Task.CompletedTask;
        };

        connection.Reconnected += (_) =>
        {
            DeviceStatusChanged?.Invoke(ipAddress, true);
            return Task.CompletedTask;
        };

        connection.On<BeaconDevice>("ReceiveDeviceDetails", (device) =>
        {
            try
            {
                if (device != null)
                    DeviceDetailsReceived?.Invoke(ipAddress, device);
            }
            catch (Exception ex)
            {
                LogHandleDetailsError(ipAddress, ex);
            }
        });

        connection.On("CloseConnection", () =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await DisconnectBeaconHubAsync(ipAddress);
                }
                catch (Exception ex)
                {
                    LogConnectionError(ipAddress, ex);
                }
            });
        });

        return connection;
    }
    private string? ResolveApiKey(string ipAddress)
    {
        var scoped = mPrefs.Get(StorageKeys.ApiKeyFor(ipAddress), string.Empty);
        if (!string.IsNullOrEmpty(scoped))
            return scoped;

        var global = mPrefs.Get(StorageKeys.ApiKey, string.Empty);
        return string.IsNullOrEmpty(global) ? null : global;
    }


    private static readonly Action<ILogger, string, string, Exception?> LogErrorAction =
            LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1, "ConnectionError"), "Connection error for {IpAddress}: {Message}");

    private static readonly Action<ILogger, string, Exception?> LogStartedAction =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "ConnectionStarted"), "Connection started for {IpAddress}");

    private static readonly Action<ILogger, string, Exception?> LogStoppedAction =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, "ConnectionStopped"), "Connection stopped for {IpAddress}");

    private static readonly Action<ILogger, string, Exception?> LogHandleDetailsErrorAction =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "HandleDetailsError"), "Failed to handle device details for {Id}");

    private static readonly Action<ILogger, string, Exception?> LogKeyForgottenAction =
    LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, "KeyForgotten"), "Removed stored pairing key for {IpAddress}");

    private void LogConnectionError(string ip, Exception ex) => LogErrorAction(mLogger, ip, ex.Message, ex);

    private void LogConnectionStarted(string ip) => LogStartedAction(mLogger, ip, null);

    private void LogConnectionStopped(string ip) => LogStoppedAction(mLogger, ip, null);

    private void LogHandleDetailsError(string ip, Exception ex) => LogHandleDetailsErrorAction(mLogger, ip, ex);

    private void LogKeyForgotten(string ip) => LogKeyForgottenAction(mLogger, ip, null);
}