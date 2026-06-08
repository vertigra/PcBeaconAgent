using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Service.Configuration;

namespace PcBeaconAgent.Service.BackgroundServices;

/// <summary>
/// Background service that listens for UDP discovery pings and responds with the agent's identity,
/// active network adapter details, hardware MAC address, and Web API configuration port.
/// </summary>
public partial class UdpBackgroundResponder : BackgroundService
{
    private readonly AppSettings mSettings;
    private readonly string mDiscoveryRequestPayload = "PING_PC_BEACON_AGENT";
    private readonly ILogger<UdpBackgroundResponder> mLogger;
    private readonly JsonSerializerOptions mJsonOptions;
    private readonly string mMachineName;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpBackgroundResponder"/> class.
    /// </summary>
    /// <param name="settings">The application settings containing server and discovery port configurations.</param>
    /// <param name="logger">The high-performance logger instance for tracking network activities.</param>
    public UdpBackgroundResponder(AppSettings settings, ILogger<UdpBackgroundResponder> logger)
    {
        mSettings = settings;
        mLogger = logger;
        mMachineName = Environment.MachineName;

        mJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int udpPort = mSettings.Server.DiscoveryPort;

        LogStartingResponder(mLogger, udpPort);

        using var udpServer = new UdpClient();

        try
        {
            udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, udpPort));

            if (OperatingSystem.IsWindows())
            {
                // SIO_UDP_CONNRESET fixes the annoying Winsock 10054 Connection Reset crash on Windows
                const int SIO_UDP_CONNRESET = -1744830452;
                udpServer.Client.IOControl(SIO_UDP_CONNRESET, [0], null);
            }

            LogResponderBound(mLogger, udpPort);
        }
        catch (Exception ex)
        {
            LogResponderLoopError(mLogger, ex);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpServer.ReceiveAsync(stoppingToken);
                string message = Encoding.UTF8.GetString(result.Buffer);

                if (message == mDiscoveryRequestPayload)
                {
                    LogDiscoveryRequestReceived(mLogger, result.RemoteEndPoint);

                    var (mac, ifaceName, ifaceType) = GetNetworkInterfaceDetails(result.RemoteEndPoint.Address);

                    var responseData = new BeaconDevice
                    {
                        MachineName = mMachineName,
                        ApiPort = mSettings.Server.Port,
                        MacAddress = mac,
                        InterfaceName = ifaceName,
                        InterfaceType = ifaceType,
                        Status = "Online",
                        LastSeen = DateTime.UtcNow
                    };

                    byte[] responseBuffer = JsonSerializer.SerializeToUtf8Bytes(responseData, BeaconJsonContext.Default.BeaconDevice);

                    var targetEndPoint = new IPEndPoint(result.RemoteEndPoint.Address, udpPort);
                    await udpServer.SendAsync(responseBuffer, responseBuffer.Length, targetEndPoint);

                    LogDiscoveryResponseSent(mLogger, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                LogConnectionResetIgnored(mLogger);
            }
            catch (Exception ex)
            {
                LogResponderLoopError(mLogger, ex);
                await Task.Delay(2000, stoppingToken);
            }
        }

        LogResponderStopped(mLogger, udpPort);
    }

    /// <summary>
    /// Analyzes available local network interfaces to extract specific hardware properties 
    /// corresponding to the subnet used by the requesting client.
    /// </summary>
    /// <param name="clientIp">The remote IP address of the discovering client device.</param>
    /// <returns>A tuple containing the MAC address, system interface name, and connectivity medium type.</returns>
    private (string Mac, string Name, string Type) GetNetworkInterfaceDetails(IPAddress clientIp)
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var ni in interfaces)
            {
                // Filter only operational network cards, excluding software loopbacks
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = ni.GetIPProperties();

                // Match interface bound to the same local subnet as the client
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    byte[] clientBytes = clientIp.GetAddressBytes();
                    byte[] serverBytes = unicast.Address.GetAddressBytes();

                    // Basic matching criteria checking the main local network octets
                    if (clientBytes[0] == serverBytes[0] && clientBytes[1] == serverBytes[1] && clientBytes[2] == serverBytes[2])
                    {
                        // Format physical MAC hardware address into standardized hex representation (AA:BB:CC:DD:EE:FF)
                        string mac = string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));

                        string type = ni.NetworkInterfaceType switch
                        {
                            NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                            NetworkInterfaceType.Ethernet => "Ethernet",
                            _ => ni.NetworkInterfaceType.ToString()
                        };

                        return (mac, ni.Name, type);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogNetworkInterfaceFailure(mLogger, ex);
        }

        return ("Unknown", "Unknown", "Unknown");
    }

    // --- High-Performance Code-Generated Logger Extensions ---

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Starting UDP Responder on port {Port}...")]
    private static partial void LogStartingResponder(ILogger logger, int port);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "UDP Responder successfully bound to port {Port}.")]
    private static partial void LogResponderBound(ILogger logger, int port);

    [LoggerMessage(EventId = 102, Level = LogLevel.Information, Message = "Discovery request received from {ClientEndpoint}")]
    private static partial void LogDiscoveryRequestReceived(ILogger logger, IPEndPoint clientEndpoint);

    [LoggerMessage(EventId = 103, Level = LogLevel.Trace, Message = "Sent JSON discovery response to {ClientEndpoint}.")]
    private static partial void LogDiscoveryResponseSent(ILogger logger, IPEndPoint clientEndpoint);

    [LoggerMessage(EventId = 104, Level = LogLevel.Debug, Message = "Ignored Windows Winsock 10054 Connection Reset error.")]
    private static partial void LogConnectionResetIgnored(ILogger logger);

    [LoggerMessage(EventId = 105, Level = LogLevel.Information, Message = "UDP Responder on port {Port} stopped.")]
    private static partial void LogResponderStopped(ILogger logger, int port);

    [LoggerMessage(EventId = 106, Level = LogLevel.Debug, Message = "Failed to retrieve network interface details.")]
    private static partial void LogNetworkInterfaceFailure(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 107, Level = LogLevel.Error, Message = "Error occurred in UDP background responder loop.")]
    private static partial void LogResponderLoopError(ILogger logger, Exception ex);
}