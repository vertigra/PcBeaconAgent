using System;
using System.Threading;
using System.Threading.Tasks;
using PcBeaconAgent.Client.Core.Models;

namespace PcBeaconAgent.Client.Core.Interfaces;

public interface ISignalService
{
    event Action<string, BeaconDevice>? DeviceDetailsReceived;
    event Action<string, bool>? DeviceStatusChanged;

    Task ConnectToBeaconHubAsync(BeaconDevice beaconDevice);
    Task DisconnectBeaconHubAsync(string ipAddress);
    Task DisconnectBeaconHubAsync(BeaconDevice beaconDevice);
    Task ForgetAsync(string ipAddress);

    Task SendCommandAsync(string ipAddress, string command, object data);
    Task SendCommandAsync(string ipAddress, string command);

    /// <summary>
    /// Establishes a connection, requests device details, and waits for the
    /// actual <c>ReceiveDeviceDetails</c> reply to arrive before returning —
    /// not just for the request to be sent. The connection is closed by the
    /// server immediately after sending the reply.
    /// </summary>
    /// <returns>The device data received from the server.</returns>
    /// <exception cref="Exceptions.NotPairedException">No API key stored for this device.</exception>
    /// <exception cref="TimeoutException">No reply within the timeout window.</exception>
    // FIX: было Task (без возвращаемого значения). SendCommandAsync внутри
    // дожидается только отправки сообщения по сети, а не обработки его сервером —
    // вызывающий код мог продолжить работу (например, RememberDevice) с устройством,
    // у которого ещё не было реальных данных. Теперь метод возвращает Task<BeaconDevice>
    // и резолвится только когда ответ действительно получен.
    Task<BeaconDevice> ReceiveDeviceDetailsAndCloseAsync(BeaconDevice beaconDevice, CancellationToken ct = default);
}