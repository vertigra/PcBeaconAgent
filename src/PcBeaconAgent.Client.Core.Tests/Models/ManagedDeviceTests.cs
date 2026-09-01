using PcBeaconAgent.Client.Core.Models;
using PcBeaconAgent.Contracts.Models;
using Xunit;

namespace PcBeaconAgent.Client.Core.Tests.Models
{
    public class ManagedDeviceTests
    {
        private static BeaconDevice MakeDevice(string ip = "10.0.0.1") => new()
        {
            MachineName = "TestPC",
            IpAddress = ip,
            ApiPort = 5000,
            MacAddress = "001122334455",
            InterfaceName = "Ethernet",
            InterfaceType = "Ethernet"
        };

        [Fact]
        public void Equals_SameDevice_ReturnsTrue()
        {
            var device = MakeDevice();
            var a = new ManagedDevice(device, null!, null!, null!, null!);
            var b = new ManagedDevice(device, null!, null!, null!, null!);

            Assert.True(a.Equals(b));
        }

        [Fact]
        public void Equals_DifferentDevice_ReturnsFalse()
        {
            var a = new ManagedDevice(MakeDevice("10.0.0.1"), null!, null!, null!, null!);
            var b = new ManagedDevice(MakeDevice("10.0.0.2"), null!, null!, null!, null!);

            Assert.False(a.Equals(b));
        }

        [Fact]
        public void Equals_Null_ReturnsFalse()
        {
            var a = new ManagedDevice(MakeDevice(), null!, null!, null!, null!);
            Assert.False(a.Equals(null));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var a = new ManagedDevice(MakeDevice(), null!, null!, null!, null!);
            Assert.True(a.Equals(a));
        }

        [Fact]
        public void GetHashCode_SameDevice_SameHash()
        {
            var device = MakeDevice();
            var a = new ManagedDevice(device, null!, null!, null!, null!);
            var b = new ManagedDevice(device, null!, null!, null!, null!);

            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void IsOnline_DefaultFalse()
        {
            var managed = new ManagedDevice(MakeDevice(), null!, null!, null!, null!);
            Assert.False(managed.IsOnline);
        }

        [Fact]
        public void IsOnline_SetTrue_RaisesPropertyChanged()
        {
            var managed = new ManagedDevice(MakeDevice(), null!, null!, null!, null!);
            bool raised = false;
            managed.PropertyChanged += (_, _) => raised = true;

            managed.IsOnline = true;

            Assert.True(raised);
            Assert.True(managed.IsOnline);
        }
    }
}
