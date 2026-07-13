using Microsoft.Extensions.Logging;
using Moq;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Services;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PcBeaconAgent.Server.Core.Tests.Services
{
    public class BeaconServerTests
    {
        [Fact]
        public async Task StartAsync_RespondsToPing_WithPongAndPort()
        {
            // Pick a random port for the test to avoid conflicts.
            int discoveryPort = GetFreePort();
            int apiPort = GetFreePort();

            var beaconOptions = new BeaconServerOptions("0.0.0.0", discoveryPort);
            var apiOptions = new WebApiOptions(apiPort, "test-key");
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<BeaconServer>>();
            var server = new BeaconServer(beaconOptions, apiOptions, logger.Object);

            using var cts = new CancellationTokenSource();

            // Start the server in the background.
            var serverTask = server.StartAsync(cts.Token);

            // Give the server a moment to bind.
            await Task.Delay(300);

            // Send a ping.
            using var client = new UdpClient();
            var ping = new byte[] { 0x01 }; // ping byte
            await client.SendAsync(ping, ping.Length, "127.0.0.1", discoveryPort);

            // Receive the response.
            var response = await client.ReceiveAsync();
            cts.Cancel();

            // Response: [pong, portLow, portHigh]
            Assert.Equal(3, response.Buffer.Length);
            Assert.Equal(0x02, response.Buffer[0]); // pong

            int returnedPort = System.BitConverter.ToUInt16(response.Buffer, 1);
            Assert.Equal(apiPort, returnedPort);

            // Wait for the server task to complete (it exits via
            // OperationCanceledException).
            try { await serverTask; }
            catch { /* expected */ }
        }

        private static int GetFreePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }
    }
}
