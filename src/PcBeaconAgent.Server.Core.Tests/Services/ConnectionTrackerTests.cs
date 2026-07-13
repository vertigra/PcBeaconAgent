using PcBeaconAgent.Server.Core.Models;
using PcBeaconAgent.Server.Core.Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PcBeaconAgent.Server.Core.Tests.Services
{
    public class ConnectionTrackerTests
    {
        [Fact]
        public void NewTracker_HasZeroConnections()
        {
            var tracker = new ConnectionTracker();
            Assert.Equal(0, tracker.ConnectedCount);
            Assert.Empty(tracker.ConnectedClients);
        }

        [Fact]
        public void Register_IncrementsCount()
        {
            var tracker = new ConnectionTracker();
            tracker.Register("conn-1", new ClientInfo { RemoteIp = "10.0.0.1" });

            Assert.Equal(1, tracker.ConnectedCount);
            Assert.Single(tracker.ConnectedClients);
        }

        [Fact]
        public void Register_StoresClientInfo()
        {
            var tracker = new ConnectionTracker();
            var info = new ClientInfo { RemoteIp = "10.0.0.1", UserAgent = "TestAgent" };
            tracker.Register("conn-1", info);

            var snapshot = tracker.ConnectedClients;
            Assert.True(snapshot.ContainsKey("conn-1"));
            Assert.Equal("10.0.0.1", snapshot["conn-1"].RemoteIp);
            Assert.Equal("TestAgent", snapshot["conn-1"].UserAgent);
        }

        [Fact]
        public void Unregister_DecrementsCount()
        {
            var tracker = new ConnectionTracker();
            tracker.Register("conn-1", new ClientInfo { RemoteIp = "10.0.0.1" });
            tracker.Register("conn-2", new ClientInfo { RemoteIp = "10.0.0.2" });

            tracker.Unregister("conn-1");

            Assert.Equal(1, tracker.ConnectedCount);
            Assert.DoesNotContain("conn-1", tracker.ConnectedClients.Keys);
            Assert.Contains("conn-2", tracker.ConnectedClients.Keys);
        }

        [Fact]
        public void Unregister_UnknownConnectionId_IsNoOp()
        {
            var tracker = new ConnectionTracker();
            tracker.Register("conn-1", new ClientInfo());

            tracker.Unregister("nonexistent");

            Assert.Equal(1, tracker.ConnectedCount);
        }

        [Fact]
        public void Register_DuplicateConnectionId_OverwritesInfo()
        {
            var tracker = new ConnectionTracker();
            tracker.Register("conn-1", new ClientInfo { RemoteIp = "10.0.0.1" });

            tracker.Register("conn-1", new ClientInfo { RemoteIp = "10.0.0.99" });

            Assert.Equal(1, tracker.ConnectedCount);
            Assert.Equal("10.0.0.99", tracker.ConnectedClients["conn-1"].RemoteIp);
        }

        [Fact]
        public void CountChanged_RaisedOnRegister()
        {
            // xUnit installs its own SynchronizationContext, which
            // makes ConnectionTracker.Post asynchronous — the callback
            // would not fire before the assertion. Null it out so the
            // tracker fires the event synchronously.
            var prevCtx = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var tracker = new ConnectionTracker();
                int? raisedCount = null;
                tracker.CountChanged += c => raisedCount = c;

                tracker.Register("conn-1", new ClientInfo());

                Assert.Equal(1, raisedCount);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevCtx);
            }
        }

        [Fact]
        public void CountChanged_RaisedOnUnregister()
        {
            var prevCtx = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var tracker = new ConnectionTracker();
                tracker.Register("conn-1", new ClientInfo());

                int? raisedCount = null;
                tracker.CountChanged += c => raisedCount = c;

                tracker.Unregister("conn-1");

                Assert.Equal(0, raisedCount);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevCtx);
            }
        }

        [Fact]
        public void CountChanged_NotRaisedWhenNoSubscribers()
        {
            var tracker = new ConnectionTracker();
            // No subscribers — should not throw.
            tracker.Register("conn-1", new ClientInfo());
            tracker.Unregister("conn-1");
            Assert.Equal(0, tracker.ConnectedCount);
        }

        [Fact]
        public void ConnectedClients_ReturnsSnapshot_NotLiveReference()
        {
            var tracker = new ConnectionTracker();
            tracker.Register("conn-1", new ClientInfo());

            var snapshot = tracker.ConnectedClients;
            tracker.Register("conn-2", new ClientInfo());

            // The snapshot should not have changed — it's a copy.
            Assert.Single(snapshot);
            Assert.Equal(2, tracker.ConnectedClients.Count);
        }

        [Fact]
        public async Task ConcurrentRegisters_AreThreadSafe()
        {
            var tracker = new ConnectionTracker();

            // Simulate 50 concurrent connections. If the locking is
            // broken, the count will be less than 50 due to lost
            // increments.
            var tasks = new Task[50];
            for (int i = 0; i < 50; i++)
            {
                int idx = i;
                tasks[i] = Task.Run(() =>
                    tracker.Register($"conn-{idx}", new ClientInfo()));
            }
            await Task.WhenAll(tasks);

            Assert.Equal(50, tracker.ConnectedCount);
        }

        [Fact]
        public async Task ConcurrentRegistersThenUnregisters_AllSucceed()
        {
            var tracker = new ConnectionTracker();

            // Phase 1: register 50 connections concurrently.
            var registerTasks = new Task[50];
            for (int i = 0; i < 50; i++)
            {
                int idx = i;
                registerTasks[i] = Task.Run(() =>
                    tracker.Register($"conn-{idx}", new ClientInfo()));
            }
            await Task.WhenAll(registerTasks);
            Assert.Equal(50, tracker.ConnectedCount);

            // Phase 2: unregister all 50 concurrently.
            var unregisterTasks = new Task[50];
            for (int i = 0; i < 50; i++)
            {
                int idx = i;
                unregisterTasks[i] = Task.Run(() =>
                    tracker.Unregister($"conn-{idx}"));
            }
            await Task.WhenAll(unregisterTasks);
            Assert.Equal(0, tracker.ConnectedCount);
        }
    }
}
