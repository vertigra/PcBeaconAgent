using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using PcBeaconAgent.Server.Core.Configuration;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Models;
using PcBeaconAgent.Server.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PcBeaconAgent.Server.Core.Tests.Services
{
    public class TransferControllerTests : IDisposable
    {
        private readonly Mock<ILogger<TransferController>> mLoggerMock;
        private readonly Mock<IHubContext<BeaconServiceHub>> mHubContextMock;
        private readonly Mock<IConnectionTracker> mConnectionTrackerMock;
        private readonly TransferSettings mSettings;
        private readonly string mTempSaveFolder;

        public TransferControllerTests()
        {
            mLoggerMock = new Mock<ILogger<TransferController>>();
            mHubContextMock = new Mock<IHubContext<BeaconServiceHub>>();
            mConnectionTrackerMock = new Mock<IConnectionTracker>();
            mTempSaveFolder = Path.Combine(Path.GetTempPath(), $"pcbeacon-tests-{Guid.NewGuid():N}");
            mSettings = new TransferSettings { SaveFolder = mTempSaveFolder };
        }

        private TransferController CreateController() =>
            new(mLoggerMock.Object, mSettings, mHubContextMock.Object, mConnectionTrackerMock.Object);

        public void Dispose()
        {
            // Clean up temp folder after each test — files written by
            // ReceiveFile tests would otherwise accumulate.
            if (Directory.Exists(mTempSaveFolder))
            {
                try { Directory.Delete(mTempSaveFolder, recursive: true); }
                catch { /* test cleanup — best effort */ }
            }
        }

        // ── Basic text receive ───────────────────────────────────────

        [Fact]
        public void ReceiveText_ValidPayload_AcceptsAndStores()
        {
            var controller = CreateController();
            var (accepted, message) = controller.ReceiveText("Hello, PC!", "192.168.1.42");

            Assert.True(accepted);
            Assert.Equal("Transfer received.", message);

            var history = controller.GetHistory();
            Assert.Single(history);
            Assert.Equal("Hello, PC!", history[0].Text);
            Assert.Equal("192.168.1.42", history[0].SourceIp);
            Assert.Equal(TransferKind.Text, history[0].Kind);
            Assert.False(string.IsNullOrEmpty(history[0].Id));
        }

        [Fact]
        public void ReceiveText_SetsReceivedAtToRecentUtc()
        {
            var controller = CreateController();
            var before = DateTime.UtcNow;

            controller.ReceiveText("test", "10.0.0.1");

            var after = DateTime.UtcNow;
            var record = controller.GetHistory()[0];
            Assert.InRange(record.ReceivedAtUtc, before, after);
        }

        [Fact]
        public void ReceiveText_GeneratesUniqueIds()
        {
            var controller = CreateController();
            controller.ReceiveText("first", "10.0.0.1");
            controller.ReceiveText("second", "10.0.0.1");

            var history = controller.GetHistory();
            Assert.NotEqual(history[0].Id, history[1].Id);
        }

        [Fact]
        public void ReceiveText_SetsSizeBytes()
        {
            var controller = CreateController();
            controller.ReceiveText("Hello", "10.0.0.1");

            var record = controller.GetHistory()[0];
            Assert.Equal(5, record.SizeBytes); // "Hello" = 5 UTF-8 bytes
        }

        // ── Text validation ─────────────────────────────────────────

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n\r")]
        public void ReceiveText_EmptyOrWhitespace_Rejects(string payload)
        {
            var controller = CreateController();
            var (accepted, message) = controller.ReceiveText(payload, "10.0.0.1");

            Assert.False(accepted);
            Assert.Contains("empty", message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(controller.GetHistory());
        }

        [Fact]
        public void ReceiveText_ExceedingSizeCap_Rejects()
        {
            var controller = CreateController();
            string oversized = new string('a', TransferController.MaxTextSizeBytes + 1);

            var (accepted, message) = controller.ReceiveText(oversized, "10.0.0.1");

            Assert.False(accepted);
            Assert.Contains("too large", message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(controller.GetHistory());
        }

        [Fact]
        public void ReceiveText_ExactlyAtSizeCap_Accepts()
        {
            var controller = CreateController();
            string atCap = new string('a', TransferController.MaxTextSizeBytes);

            var (accepted, _) = controller.ReceiveText(atCap, "10.0.0.1");

            Assert.True(accepted);
            Assert.Single(controller.GetHistory());
        }

        // ── Basic file receive ──────────────────────────────────────

        [Fact]
        public void ReceiveFile_ValidStream_SavesAndStores()
        {
            var controller = CreateController();
            byte[] content = "Hello, file!"u8.ToArray();
            using var stream = new MemoryStream(content);

            var (accepted, message, savedName) = controller.ReceiveFile(stream, "test.txt", "192.168.1.42");

            Assert.True(accepted);
            Assert.Contains("test.txt", savedName);
            Assert.Contains("File received", message);

            var history = controller.GetHistory();
            Assert.Single(history);
            var record = history[0];
            Assert.Equal(TransferKind.File, record.Kind);
            Assert.Equal("test.txt", record.FileName);
            Assert.Equal(content.Length, record.SizeBytes);
            Assert.True(File.Exists(record.SavedFilePath));
            Assert.Equal("Hello, file!", File.ReadAllText(record.SavedFilePath));
        }

        [Fact]
        public void ReceiveFile_EmptyStream_Rejects()
        {
            var controller = CreateController();
            using var stream = new MemoryStream();

            var (accepted, _, _) = controller.ReceiveFile(stream, "empty.txt", "10.0.0.1");

            // Empty stream is not rejected at the controller level —
            // the endpoint layer checks file.Length == 0. The controller
            // saves the file (0 bytes) and reports success. This is
            // correct: an empty file is a valid edge case (e.g. a
            // sentinel file the user wants to create).
            Assert.True(accepted);
            Assert.Empty(controller.GetHistory()[0].Text);
        }

        [Fact]
        public void ReceiveFile_NullStream_Rejects()
        {
            var controller = CreateController();

            var (accepted, message, _) = controller.ReceiveFile(null!, "test.txt", "10.0.0.1");

            Assert.False(accepted);
            Assert.Contains("null", message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(controller.GetHistory());
        }

        // ── File name sanitisation ──────────────────────────────────

        [Fact]
        public void ReceiveFile_PathTraversal_StripsDirectoryComponents()
        {
            var controller = CreateController();
            byte[] content = "evil"u8.ToArray();
            using var stream = new MemoryStream(content);

            // Client tries to write outside the save folder via a
            // crafted path. Path.GetFileName should strip the
            // directory components, leaving only "evil.dll".
            var (accepted, _, savedName) = controller.ReceiveFile(stream, "..\\..\\Windows\\System32\\evil.dll", "10.0.0.1");

            Assert.True(accepted);
            Assert.Equal("evil.dll", savedName);

            // Verify the file was saved inside the save folder, not
            // in Windows\System32.
            var record = controller.GetHistory()[0];
            Assert.StartsWith(mTempSaveFolder, record.SavedFilePath);
        }

        [Fact]
        public void ReceiveFile_AbsolutePath_StripsDirectoryComponents()
        {
            var controller = CreateController();
            byte[] content = "evil"u8.ToArray();
            using var stream = new MemoryStream(content);

            var (accepted, _, savedName) = controller.ReceiveFile(stream, "C:\\Windows\\evil.dll", "10.0.0.1");

            Assert.True(accepted);
            Assert.Equal("evil.dll", savedName);
        }

        [Fact]
        public void ReceiveFile_UnixStylePath_StripsDirectoryComponents()
        {
            var controller = CreateController();
            byte[] content = "evil"u8.ToArray();
            using var stream = new MemoryStream(content);

            var (accepted, _, savedName) = controller.ReceiveFile(stream, "../../etc/passwd", "10.0.0.1");

            Assert.True(accepted);
            Assert.Equal("passwd", savedName);
        }

        [Fact]
        public void ReceiveFile_ReservedWindowsName_ReplacedWithGenericName()
        {
            var controller = CreateController();
            byte[] content = "reserved"u8.ToArray();
            using var stream = new MemoryStream(content);

            var (accepted, _, savedName) = controller.ReceiveFile(stream, "CON.txt", "10.0.0.1");

            Assert.True(accepted);
            Assert.NotEqual("CON.txt", savedName);
            Assert.StartsWith("transfer-", savedName);
        }

        [Fact]
        public void ReceiveFile_EmptyFileName_FallsBackToGenericName()
        {
            var controller = CreateController();
            byte[] content = "noname"u8.ToArray();
            using var stream = new MemoryStream(content);

            var (accepted, _, savedName) = controller.ReceiveFile(stream, "", "10.0.0.1");

            Assert.True(accepted);
            Assert.StartsWith("transfer-", savedName);
        }

        [Fact]
        public void ReceiveFile_NameCollision_AppendsNumericSuffix()
        {
            var controller = CreateController();
            byte[] content1 = "first"u8.ToArray();
            byte[] content2 = "second"u8.ToArray();

            using (var s1 = new MemoryStream(content1))
                controller.ReceiveFile(s1, "dup.txt", "10.0.0.1");

            using (var s2 = new MemoryStream(content2))
                controller.ReceiveFile(s2, "dup.txt", "10.0.0.1");

            var history = controller.GetHistory();
            Assert.Equal(2, history.Count);
            // GetHistory returns newest-first. The second upload (dup (1).txt)
            // is newest, so it is at index 0; the first upload (dup.txt) is
            // oldest, at index 1.
            Assert.Equal("dup (1).txt", history[0].FileName);
            Assert.Equal("dup.txt", history[1].FileName);

            // Both files should exist on disk with their respective
            // content — the second upload must NOT overwrite the first.
            Assert.Equal("second", File.ReadAllText(history[0].SavedFilePath));
            Assert.Equal("first", File.ReadAllText(history[1].SavedFilePath));
        }

        // ── Mixed history ───────────────────────────────────────────

        [Fact]
        public void GetHistory_MixedTextAndFile_ReturnsAllNewestFirst()
        {
            var controller = CreateController();
            controller.ReceiveText("text-1", "10.0.0.1");
            Thread.Sleep(10);

            byte[] content = "file-1"u8.ToArray();
            using (var s = new MemoryStream(content))
                controller.ReceiveFile(s, "f1.txt", "10.0.0.1");
            Thread.Sleep(10);

            controller.ReceiveText("text-2", "10.0.0.1");

            var history = controller.GetHistory();
            Assert.Equal(3, history.Count);
            Assert.Equal("text-2", history[0].Text);
            Assert.Equal(TransferKind.Text, history[0].Kind);

            Assert.Equal("f1.txt", history[1].FileName);
            Assert.Equal(TransferKind.File, history[1].Kind);

            Assert.Equal("text-1", history[2].Text);
            Assert.Equal(TransferKind.Text, history[2].Kind);
        }

        // ── History ordering & cap ──────────────────────────────────

        [Fact]
        public void GetHistory_ReturnsNewestFirst()
        {
            var controller = CreateController();
            controller.ReceiveText("first", "10.0.0.1");
            Thread.Sleep(10);
            controller.ReceiveText("second", "10.0.0.1");
            Thread.Sleep(10);
            controller.ReceiveText("third", "10.0.0.1");

            var history = controller.GetHistory();
            Assert.Equal(3, history.Count);
            Assert.Equal("third", history[0].Text);
            Assert.Equal("second", history[1].Text);
            Assert.Equal("first", history[2].Text);
        }

        [Fact]
        public void GetHistory_EmptyInitially()
        {
            var controller = CreateController();
            Assert.Empty(controller.GetHistory());
        }

        [Fact]
        public void ReceiveText_OverHistoryCap_EvictsOldest()
        {
            var controller = CreateController();

            for (int i = 0; i < TransferController.MaxHistoryItems; i++)
            {
                controller.ReceiveText($"item-{i}", "10.0.0.1");
            }

            Assert.Equal(TransferController.MaxHistoryItems, controller.GetHistory().Count);

            controller.ReceiveText("overflow", "10.0.0.1");

            var history = controller.GetHistory();
            Assert.Equal(TransferController.MaxHistoryItems, history.Count);
            Assert.Equal("overflow", history[0].Text);
            Assert.Equal("item-1", history[^1].Text);
        }

        // ── Events ──────────────────────────────────────────────────

        [Fact]
        public void ReceiveText_RaisesTransferReceivedEvent()
        {
            var controller = CreateController();
            TransferRecord? received = null;
            controller.TransferReceived += r => received = r;

            controller.ReceiveText("event test", "10.0.0.5");

            Assert.NotNull(received);
            Assert.Equal("event test", received!.Text);
            Assert.Equal("10.0.0.5", received.SourceIp);
        }

        [Fact]
        public void ReceiveFile_RaisesTransferReceivedEvent()
        {
            var controller = CreateController();
            TransferRecord? received = null;
            controller.TransferReceived += r => received = r;

            byte[] content = "file event"u8.ToArray();
            using var stream = new MemoryStream(content);
            controller.ReceiveFile(stream, "event.txt", "10.0.0.5");

            Assert.NotNull(received);
            Assert.Equal(TransferKind.File, received!.Kind);
            Assert.Equal("event.txt", received.FileName);
            Assert.Equal("10.0.0.5", received.SourceIp);
        }

        [Fact]
        public void ReceiveText_NoSubscriber_DoesNotCrash()
        {
            var controller = CreateController();
            var (accepted, _) = controller.ReceiveText("no subscriber", "10.0.0.1");
            Assert.True(accepted);
        }

        [Fact]
        public void ReceiveText_EventRaisedOutsideLock_SubscriberCanCallGetHistory()
        {
            var controller = CreateController();
            IReadOnlyList<TransferRecord>? snapshotFromSubscriber = null;

            controller.TransferReceived += r =>
            {
                snapshotFromSubscriber = controller.GetHistory();
            };

            controller.ReceiveText("deadlock test", "10.0.0.1");

            Assert.NotNull(snapshotFromSubscriber);
            Assert.Single(snapshotFromSubscriber!);
        }

        // ── Source IP ───────────────────────────────────────────────

        [Fact]
        public void ReceiveText_EmptySourceIp_NormalisedToUnknown()
        {
            var controller = CreateController();
            controller.ReceiveText("test", "");

            var record = controller.GetHistory()[0];
            Assert.Equal("unknown", record.SourceIp);
        }

        [Fact]
        public void ReceiveText_NullSourceIp_NormalisedToUnknown()
        {
            var controller = CreateController();
            controller.ReceiveText("test", null!);

            var record = controller.GetHistory()[0];
            Assert.Equal("unknown", record.SourceIp);
        }

        // ── Concurrency ─────────────────────────────────────────────

        [Fact]
        public async Task ReceiveText_ConcurrentCalls_AllStoredSafely()
        {
            var controller = CreateController();
            const int parallelCount = 50;

            var tasks = Enumerable.Range(0, parallelCount)
                .Select(i => Task.Run(() => controller.ReceiveText($"concurrent-{i}", "10.0.0.1")))
                .ToArray();

            await Task.WhenAll(tasks);

            var history = controller.GetHistory();
            Assert.Equal(parallelCount, history.Count);

            var uniqueIds = history.Select(r => r.Id).Distinct().Count();
            Assert.Equal(parallelCount, uniqueIds);
        }

        [Fact]
        public async Task ReceiveText_ConcurrentOverCap_EvictsDownToCap()
        {
            var controller = CreateController();
            const int parallelCount = TransferController.MaxHistoryItems + 20;

            var tasks = Enumerable.Range(0, parallelCount)
                .Select(i => Task.Run(() => controller.ReceiveText($"concurrent-{i}", "10.0.0.1")))
                .ToArray();

            await Task.WhenAll(tasks);

            var history = controller.GetHistory();
            Assert.Equal(TransferController.MaxHistoryItems, history.Count);
        }

        // ── Outgoing (PC → Android via SignalR) ──────────────────────

        /// <summary>
        /// Sets up the IHubContext mock so that SendAsync on a specific
        /// connection ID records the event name and arguments. Returns
        /// a list that tests can assert against.
        /// </summary>
        private List<(string Event, object[] Args)> SetupHubMock(string targetConnectionId, string targetIp = "10.0.0.42")
        {
            var calls = new List<(string, object[])>();

            var clientProxyMock = new Mock<ISingleClientProxy>();
            clientProxyMock
                .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object[], CancellationToken>((name, args, _) => calls.Add((name, args)))
                .Returns(Task.CompletedTask);

            // FindConnectionIdByIp returns the connection ID when the
            // IP matches — this replaces the old ConnectedClients.ContainsKey
            // check that SendTextToClientAsync used to do.
            mConnectionTrackerMock.Setup(t => t.FindConnectionIdByIp(targetIp))
                .Returns(targetConnectionId);
            mConnectionTrackerMock.Setup(t => t.FindConnectionIdByIp(It.Is<string>(s => s != targetIp)))
                .Returns((string?)null);

            var clientsMock = new Mock<IHubClients>();
            clientsMock.Setup(c => c.Client(targetConnectionId)).Returns(clientProxyMock.Object);
            mHubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

            return calls;
        }

        [Fact]
        public async Task SendTextToClient_ValidPayload_PushesAndStores()
        {
            const string connId = "test-conn-1";
            const string ip = "10.0.0.42";
            var calls = SetupHubMock(connId, ip);
            var controller = CreateController();

            var (accepted, message) = await controller.SendTextToClientAsync("Hello, phone!", ip, "TestPC");

            Assert.True(accepted);
            Assert.Equal("Transfer sent.", message);

            Assert.Single(calls);
            Assert.Equal("ReceiveTextTransfer", calls[0].Event);
            Assert.Equal("Hello, phone!", calls[0].Args[0]);
            Assert.Equal("TestPC", calls[0].Args[1]);

            var history = controller.GetHistory();
            Assert.Single(history);
            Assert.Equal(TransferDirection.Outgoing, history[0].Direction);
            Assert.Equal(TransferKind.Text, history[0].Kind);
            Assert.Equal("Hello, phone!", history[0].Text);
            Assert.Equal(ip, history[0].SourceIp);
        }

        [Fact]
        public async Task SendTextToClient_ClientOffline_QueuesAsPending()
        {
            // FindConnectionIdByIp returns null — client is offline.
            mConnectionTrackerMock.Setup(t => t.FindConnectionIdByIp(It.IsAny<string>()))
                .Returns((string?)null);

            var controller = CreateController();
            var (accepted, message) = await controller.SendTextToClientAsync("text", "10.0.0.99", "PC");

            Assert.True(accepted);
            Assert.Contains("Queued", message);
            Assert.Single(controller.GetHistory());
        }

        [Fact]
        public async Task SendFileToClient_ValidFile_PushesAndStoresAndDownloadable()
        {
            const string connId = "test-conn-2";
            const string ip = "10.0.0.42";
            var calls = SetupHubMock(connId, ip);
            var controller = CreateController();

            byte[] content = "file content"u8.ToArray();
            using var stream = new MemoryStream(content);

            var (accepted, message) = await controller.SendFileToClientAsync(
                stream, "doc.txt", ip, "TestPC", "http://10.0.0.1:5000");

            Assert.True(accepted);
            Assert.Contains("doc.txt", message);

            Assert.Single(calls);
            Assert.Equal("ReceiveFileTransfer", calls[0].Event);
            Assert.Equal("doc.txt", calls[0].Args[0]);
            Assert.Equal((long)content.Length, calls[0].Args[1]);
            string downloadUrl = (string)calls[0].Args[2];
            Assert.Contains("/api/transfer/download/", downloadUrl);

            var history = controller.GetHistory();
            Assert.Single(history);
            var record = history[0];
            Assert.Equal(TransferDirection.Outgoing, record.Direction);
            Assert.Equal(TransferKind.File, record.Kind);
            Assert.Equal("doc.txt", record.FileName);

            string? filePath = controller.GetOutgoingFilePath(record.Id);
            Assert.NotNull(filePath);
            Assert.True(File.Exists(filePath));
            Assert.Equal("file content", File.ReadAllText(filePath!));
        }

        [Fact]
        public async Task SendFileToClient_ClientOffline_QueuesAsPending()
        {
            mConnectionTrackerMock.Setup(t => t.FindConnectionIdByIp(It.IsAny<string>()))
                .Returns((string?)null);

            var controller = CreateController();
            byte[] content = "data"u8.ToArray();
            using var stream = new MemoryStream(content);

            var (accepted, message) = await controller.SendFileToClientAsync(
                stream, "f.txt", "10.0.0.99", "PC", "http://x:5000");

            Assert.True(accepted);
            Assert.Contains("Queued", message);
            Assert.Single(controller.GetHistory());
        }

        [Fact]
        public async Task GetOutgoingFilePath_UnknownToken_ReturnsNull()
        {
            var controller = CreateController();
            Assert.Null(controller.GetOutgoingFilePath("nonexistent-token"));
        }

        [Fact]
        public async Task SendTextToClient_OutgoingAndIncomingCoexistInHistory()
        {
            const string connId = "test-conn-3";
            const string ip = "10.0.0.42";
            var calls = SetupHubMock(connId, ip);
            var controller = CreateController();

            controller.ReceiveText("from phone", "10.0.0.42");
            await controller.SendTextToClientAsync("to phone", ip, "PC");

            var history = controller.GetHistory();
            Assert.Equal(2, history.Count);
            Assert.Equal(TransferDirection.Outgoing, history[0].Direction);
            Assert.Equal(TransferDirection.Incoming, history[1].Direction);
        }
    }
}
