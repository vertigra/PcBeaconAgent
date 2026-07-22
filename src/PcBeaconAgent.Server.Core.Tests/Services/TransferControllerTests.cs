using Microsoft.Extensions.Logging;
using Moq;
using PcBeaconAgent.Server.Core.Models;
using PcBeaconAgent.Server.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PcBeaconAgent.Server.Core.Tests.Services
{
    public class TransferControllerTests
    {
        private readonly Mock<ILogger<TransferController>> mLoggerMock;

        public TransferControllerTests()
        {
            mLoggerMock = new Mock<ILogger<TransferController>>();
        }

        private TransferController CreateController() => new(mLoggerMock.Object);

        // ── Basic receive ──────────────────────────────────────────

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

        // ── Validation ────────────────────────────────────────────

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
            // Build a payload that is 1 byte over the cap. Using ASCII
            // so char count == byte count, making the test deterministic.
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

        // ── History ordering ──────────────────────────────────────

        [Fact]
        public void GetHistory_ReturnsNewestFirst()
        {
            var controller = CreateController();
            controller.ReceiveText("first", "10.0.0.1");
            Thread.Sleep(10); // Ensure ReceivedAtUtc differs
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

        // ── History cap ───────────────────────────────────────────

        [Fact]
        public void ReceiveText_OverHistoryCap_EvictsOldest()
        {
            var controller = CreateController();

            // Fill to exactly the cap.
            for (int i = 0; i < TransferController.MaxHistoryItems; i++)
            {
                controller.ReceiveText($"item-{i}", "10.0.0.1");
            }

            Assert.Equal(TransferController.MaxHistoryItems, controller.GetHistory().Count);

            // One more — should evict the oldest (item-0).
            controller.ReceiveText("overflow", "10.0.0.1");

            var history = controller.GetHistory();
            Assert.Equal(TransferController.MaxHistoryItems, history.Count);

            // The newest item should be "overflow".
            Assert.Equal("overflow", history[0].Text);

            // The oldest surviving item should be "item-1" (item-0 evicted).
            Assert.Equal("item-1", history[^1].Text);
        }

        // ── Events ────────────────────────────────────────────────

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
        public void ReceiveText_NoSubscriber_DoesNotCrash()
        {
            var controller = CreateController();
            // No subscriber — event?.Invoke should handle null.
            var (accepted, _) = controller.ReceiveText("no subscriber", "10.0.0.1");
            Assert.True(accepted);
        }

        [Fact]
        public void ReceiveText_EventRaisedOutsideLock_SubscriberCanCallGetHistory()
        {
            // If the event were raised inside the lock, the subscriber
            // calling GetHistory (which also acquires the lock) would
            // deadlock or throw. This test verifies the raise-outside-
            // -lock pattern works.
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

        // ── Source IP ─────────────────────────────────────────────

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

        // ── Concurrency ───────────────────────────────────────────

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

            // Verify no corruption: all IDs should be unique.
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
            // The cap should hold — never exceed MaxHistoryItems, even
            // under concurrent insertion.
            Assert.Equal(TransferController.MaxHistoryItems, history.Count);
        }
    }
}
