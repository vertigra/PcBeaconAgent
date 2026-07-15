using Moq;
using PcBeaconAgent.Client.Core.Interfaces;
using PcBeaconAgent.Client.Core.Stores;
using PcBeaconAgent.Contracts.Models;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PcBeaconAgent.Client.Core.Tests.Stores
{
    public class DeviceStoreTests
    {
        private readonly Mock<IDeviceStorageService> mStorageMock;
        private readonly Mock<DeviceFactory> mFactoryMock;

        public DeviceStoreTests()
        {
            mStorageMock = new Mock<IDeviceStorageService>();
            mStorageMock.Setup(s => s.LoadDevices()).Returns([]);

            mFactoryMock = new Mock<DeviceFactory>(
                new Mock<System.Net.Http.IHttpClientFactory>().Object,
                new Mock<IPreferencesService>().Object);
            mFactoryMock.Setup(f => f.Create(It.IsAny<BeaconDevice>()))
                .Returns((BeaconDevice d) => new Models.ManagedDevice(d, null!, null!));
        }

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
        public void NewStore_HasNoDevices()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);
            Assert.Empty(store.ManagedDevices);
        }

        [Fact]
        public void RememberDevice_AddsDevice()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);
            var managed = store.RememberDevice(MakeDevice());

            Assert.Single(store.ManagedDevices);
            Assert.Equal("10.0.0.1", managed.Device.IpAddress);
        }

        [Fact]
        public void RememberDevice_Duplicate_DoesNotAdd()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);
            var device = MakeDevice();

            store.RememberDevice(device);
            var second = store.RememberDevice(device);

            Assert.Single(store.ManagedDevices);
            Assert.Same(store.ManagedDevices[0], second);
        }

        [Fact]
        public void RememberDevice_DifferentIP_AddsSeparately()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);

            store.RememberDevice(MakeDevice("10.0.0.1"));
            store.RememberDevice(MakeDevice("10.0.0.2"));

            Assert.Equal(2, store.ManagedDevices.Count);
        }

        [Fact]
        public void ForgetDevice_RemovesDevice()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);
            var device = MakeDevice();
            store.RememberDevice(device);

            store.ForgetDevice(device);

            Assert.Empty(store.ManagedDevices);
        }

        [Fact]
        public void ForgetDevice_NotInStore_DoesNothing()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);
            store.ForgetDevice(MakeDevice());
            Assert.Empty(store.ManagedDevices);
        }

        [Fact]
        public void RememberDevice_Persists()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);
            store.RememberDevice(MakeDevice());

            mStorageMock.Verify(s => s.SaveDevices(It.IsAny<IEnumerable<BeaconDevice>>()), Times.Once);
        }

        [Fact]
        public void ForgetDevice_Persists()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);
            var device = MakeDevice();
            store.RememberDevice(device);
            mStorageMock.Reset();

            store.ForgetDevice(device);

            mStorageMock.Verify(s => s.SaveDevices(It.IsAny<IEnumerable<BeaconDevice>>()), Times.Once);
        }

        [Fact]
        public void Constructor_LoadsSavedDevices()
        {
            var saved = new[] { MakeDevice("10.0.0.1"), MakeDevice("10.0.0.2") };
            mStorageMock.Setup(s => s.LoadDevices()).Returns(saved);

            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);

            Assert.Equal(2, store.ManagedDevices.Count);
        }

        [Fact]
        public void Constructor_DuplicateSavedDevices_NotDuplicated()
        {
            var device = MakeDevice("10.0.0.1");
            mStorageMock.Setup(s => s.LoadDevices()).Returns(new[] { device, device });

            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);

            Assert.Single(store.ManagedDevices);
        }

        [Fact]
        public void RememberDevice_PersistsCorrectDeviceCount()
        {
            var store = new DeviceStore(mFactoryMock.Object, mStorageMock.Object);
            store.RememberDevice(MakeDevice("10.0.0.1"));
            store.RememberDevice(MakeDevice("10.0.0.2"));

            mStorageMock.Verify(s => s.SaveDevices(
                It.Is<IEnumerable<BeaconDevice>>(devs => devs.Count() == 2)),
                Times.AtLeastOnce);
        }
    }
}
