using Microsoft.Extensions.Logging;
using Moq;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Services;
using System;
using System.IO;
using Xunit;

namespace PcBeaconAgent.Server.Core.Tests.Services
{
    public class BeaconServerIdentityTests : IDisposable
    {
        private readonly string mTempDir;
        private readonly Mock<ILogger<BeaconServerIdentity>> mLoggerMock;

        public BeaconServerIdentityTests()
        {
            mTempDir = Path.Combine(Path.GetTempPath(), $"pcb-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(mTempDir);
            mLoggerMock = new Mock<ILogger<BeaconServerIdentity>>();
        }

        public void Dispose()
        {
            try { Directory.Delete(mTempDir, recursive: true); }
            catch { /* temp cleanup is best-effort */ }
        }

        [Fact]
        public void Constructor_WithStaticApiKey_UsesProvidedKey()
        {
            var options = new WebApiOptions(5000, "my-static-key");

            var identity = new BeaconServerIdentity(options, mLoggerMock.Object);

            Assert.Equal("my-static-key", identity.ApiKey);
            Assert.Equal(5000, identity.ApiPort);
        }

        [Fact]
        public void Constructor_WithEmptyApiKey_LoadsFromFile()
        {
            string keyPath = Path.Combine(mTempDir, "server.key");
            File.WriteAllText(keyPath, "key-from-file");

            // Change to temp dir so LoadOrCreateKey("server.key")
            // resolves to our temp file.
            Directory.SetCurrentDirectory(mTempDir);

            var options = new WebApiOptions(5000, "");
            var identity = new BeaconServerIdentity(options, mLoggerMock.Object);

            Assert.Equal("key-from-file", identity.ApiKey);
        }

        [Fact]
        public void Constructor_WithEmptyApiKey_GeneratesNewKeyIfFileMissing()
        {
            Directory.SetCurrentDirectory(mTempDir);

            var options = new WebApiOptions(5000, "");
            var identity = new BeaconServerIdentity(options, mLoggerMock.Object);

            Assert.False(string.IsNullOrEmpty(identity.ApiKey));
            Assert.True(File.Exists("server.key"));
            Assert.Equal(identity.ApiKey, File.ReadAllText("server.key"));
        }

        [Fact]
        public void Constructor_GeneratedKey_Is32CharHexGuid()
        {
            Directory.SetCurrentDirectory(mTempDir);

            var options = new WebApiOptions(5000, "");
            var identity = new BeaconServerIdentity(options, mLoggerMock.Object);

            // Guid.ToString("N") produces 32 hex chars without dashes.
            Assert.Equal(32, identity.ApiKey.Length);
            Assert.All(identity.ApiKey, c => Assert.True(Uri.IsHexDigit(c)));
        }

        [Fact]
        public void Constructor_LoadedKey_IsTrimmed()
        {
            string keyPath = Path.Combine(mTempDir, "server.key");
            File.WriteAllText(keyPath, "  key-with-whitespace  \n");

            Directory.SetCurrentDirectory(mTempDir);

            var options = new WebApiOptions(5000, "");
            var identity = new BeaconServerIdentity(options, mLoggerMock.Object);

            Assert.Equal("key-with-whitespace", identity.ApiKey);
        }

        [Fact]
        public void Constructor_GeneratedKey_PersistsAcrossInstances()
        {
            Directory.SetCurrentDirectory(mTempDir);

            var options = new WebApiOptions(5000, "");
            var identity1 = new BeaconServerIdentity(options, mLoggerMock.Object);
            string firstKey = identity1.ApiKey;

            // Second instance should load the same key from the file
            // the first instance created.
            var identity2 = new BeaconServerIdentity(options, mLoggerMock.Object);

            Assert.Equal(firstKey, identity2.ApiKey);
        }
    }
}
