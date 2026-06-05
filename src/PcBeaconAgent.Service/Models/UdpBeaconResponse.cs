namespace PcBeaconAgent.Service.Models;

/// <summary>
/// Represents the payload sent by the agent in response to a UDP discovery ping.
/// </summary>
public class UdpBeaconResponse
{
    /// <summary>
    /// Gets or sets the network host name of the computer.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port number where the Web API is listening.
    /// </summary>
    public int ApiPort { get; set; } = 5000;
}