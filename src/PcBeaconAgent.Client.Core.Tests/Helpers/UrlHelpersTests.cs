using PcBeaconAgent.Client.Core.Helpres;
using Xunit;

namespace PcBeaconAgent.Client.Core.Tests.Helpers
{
    public class UrlHelpersTests
    {
        [Theory]
        [InlineData("10.0.0.1", 5000, "audio/devices", "http://10.0.0.1:5000/api/audio/devices")]
        [InlineData("192.168.1.100", 8080, "display/list", "http://192.168.1.100:8080/api/display/list")]
        [InlineData("localhost", 15000, "pair", "http://localhost:15000/api/pair")]
        public void BuildUrl_ReturnsCorrectUrl(string ip, int port, string path, string expected)
        {
            string result = UrlHelpers.BuildUrl(ip, port, path);
            Assert.Equal(expected, result);
        }
    }
}
