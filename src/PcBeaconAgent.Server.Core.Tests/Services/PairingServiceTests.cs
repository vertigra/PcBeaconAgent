using Microsoft.Extensions.Logging;
using Moq;
using PcBeaconAgent.Server.Core.Events;
using PcBeaconAgent.Server.Core.Interfaces;
using PcBeaconAgent.Server.Core.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PcBeaconAgent.Server.Core.Tests.Services
{
    public class PairingServiceTests
    {
        private readonly Mock<IBeaconServerIdentity> mIdentityMock;
        private readonly Mock<ILogger<PairingService>> mLoggerMock;
        private const string TestApiKey = "test-api-key-12345";

        public PairingServiceTests()
        {
            mIdentityMock = new Mock<IBeaconServerIdentity>();
            mIdentityMock.SetupGet(i => i.ApiKey).Returns(TestApiKey);
            mLoggerMock = new Mock<ILogger<PairingService>>();
        }

        private PairingService CreateService() => new(mIdentityMock.Object, mLoggerMock.Object);

        // ── Basic state ──────────────────────────────────────────

        [Fact]
        public void NewService_HasNoActivePin()
        {
            var svc = CreateService();
            Assert.False(svc.IsPairingActive);
            Assert.Equal(string.Empty, svc.GetCurrentPin());
            Assert.Null(svc.GetCurrentPinExpiryUtc());
        }

        // ── RegeneratePin ───────────────────────────────────────

        [Fact]
        public void RegeneratePin_ActivatesPairing()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            Assert.True(svc.IsPairingActive);
            Assert.False(string.IsNullOrEmpty(svc.GetCurrentPin()));
        }

        [Fact]
        public void RegeneratePin_GeneratesSixDigitPin()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();
            Assert.Equal(6, pin.Length);
            Assert.All(pin, c => Assert.True(char.IsDigit(c)));
        }

        [Fact]
        public void RegeneratePin_SetsExpiryFiveMinutesAhead()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            DateTime? expiry = svc.GetCurrentPinExpiryUtc();
            Assert.NotNull(expiry);
            double minutes = (expiry!.Value - DateTime.UtcNow).TotalMinutes;
            Assert.InRange(minutes, 4.9, 5.1);
        }

        [Fact]
        public void RegeneratePin_GeneratesDifferentPinEachTime()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            string pin1 = svc.GetCurrentPin();
            svc.RegeneratePin();
            string pin2 = svc.GetCurrentPin();
            Assert.NotEqual(pin1, pin2);
        }

        // ── Events ──────────────────────────────────────────────

        [Fact]
        public void RegeneratePin_RaisesGeneratedEvent()
        {
            var svc = CreateService();
            PairingStateEventArgs? args = null;
            svc.PairingStateChanged += e => args = e;

            svc.RegeneratePin();

            Assert.NotNull(args);
            Assert.Equal(PairingState.Generated, args!.State);
            Assert.False(string.IsNullOrEmpty(args.Pin));
            Assert.True(args.ExpiryUtc > DateTime.UtcNow);
        }

        [Fact]
        public void ValidateAndExchangePin_CorrectPin_RaisesUsedEvent()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();

            PairingStateEventArgs? args = null;
            svc.PairingStateChanged += e => args = e;

            string? apiKey = svc.ValidateAndExchangePin(pin);

            Assert.NotNull(args);
            Assert.Equal(PairingState.Used, args!.State);
            Assert.Equal(TestApiKey, apiKey);
        }

        [Fact]
        public void ValidateAndExchangePin_CorrectPin_DeactivatesPairing()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();

            svc.ValidateAndExchangePin(pin);

            Assert.False(svc.IsPairingActive);
            Assert.Equal(string.Empty, svc.GetCurrentPin());
        }

        // ── Single-use ──────────────────────────────────────────

        [Fact]
        public void ValidateAndExchangePin_PinIsSingleUse()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();

            string? apiKey1 = svc.ValidateAndExchangePin(pin);
            string? apiKey2 = svc.ValidateAndExchangePin(pin);

            Assert.Equal(TestApiKey, apiKey1);
            Assert.Null(apiKey2);
        }

        // ── Invalid PIN ─────────────────────────────────────────

        [Fact]
        public void ValidateAndExchangePin_WrongPin_ReturnsNull()
        {
            var svc = CreateService();
            svc.RegeneratePin();

            string? apiKey = svc.ValidateAndExchangePin("000000");

            Assert.Null(apiKey);
            Assert.True(svc.IsPairingActive);
        }

        [Fact]
        public void ValidateAndExchangePin_PinIsTrimmed()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();

            // Add whitespace — should still validate.
            string? apiKey = svc.ValidateAndExchangePin($"  {pin}  ");

            Assert.Equal(TestApiKey, apiKey);
        }

        // ── Lockout ─────────────────────────────────────────────

        [Fact]
        public void ValidateAndExchangePin_FiveFailedAttempts_LocksAndRaisesLockedEvent()
        {
            var svc = CreateService();
            svc.RegeneratePin();

            PairingStateEventArgs? lockedArgs = null;
            svc.PairingStateChanged += e => { if (e.State == PairingState.Locked) lockedArgs = e; };

            // 5 wrong attempts → lock on the 5th.
            for (int i = 0; i < 5; i++)
            {
                svc.ValidateAndExchangePin("000000");
            }

            Assert.False(svc.IsPairingActive);
            Assert.NotNull(lockedArgs);
            Assert.Equal(PairingState.Locked, lockedArgs!.State);
        }

        [Fact]
        public void ValidateAndExchangePin_AfterLock_RejectsCorrectPin()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();

            // Lock it.
            for (int i = 0; i < 5; i++)
                svc.ValidateAndExchangePin("000000");

            // Correct pin should now fail — locked.
            string? apiKey = svc.ValidateAndExchangePin(pin);
            Assert.Null(apiKey);
        }

        // ── Inactive state ──────────────────────────────────────

        [Fact]
        public void ValidateAndExchangePin_WhenInactive_ReturnsNull()
        {
            var svc = CreateService();
            // No PIN generated → inactive.
            string? apiKey = svc.ValidateAndExchangePin("123456");
            Assert.Null(apiKey);
        }

        // ── Regenerate resets failed attempts ───────────────────

        [Fact]
        public void RegeneratePin_ResetsFailedAttemptCounter()
        {
            var svc = CreateService();
            svc.RegeneratePin();

            // 3 failed attempts.
            svc.ValidateAndExchangePin("000000");
            svc.ValidateAndExchangePin("000000");
            svc.ValidateAndExchangePin("000000");

            // Regenerate → counter reset.
            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();

            // Should not be locked — 5 more attempts available.
            Assert.True(svc.IsPairingActive);

            // Correct PIN works.
            string? apiKey = svc.ValidateAndExchangePin(pin);
            Assert.Equal(TestApiKey, apiKey);
        }

        // ── Event sequence ──────────────────────────────────────

        [Fact]
        public void EventSequence_GenerateThenValidate_ProducesGeneratedThenUsed()
        {
            var svc = CreateService();
            var states = new System.Collections.Generic.List<PairingState>();
            svc.PairingStateChanged += e => states.Add(e.State);

            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();
            svc.ValidateAndExchangePin(pin);

            Assert.Equal(2, states.Count);
            Assert.Equal(PairingState.Generated, states[0]);
            Assert.Equal(PairingState.Used, states[1]);
        }

        // ── Regenerate cancels previous expiry ──────────────────

        [Fact]
        public async Task RegeneratePin_Twice_DoesNotFireExpiredForFirst()
        {
            var svc = CreateService();
            var states = new System.Collections.Generic.List<PairingState>();
            svc.PairingStateChanged += e => states.Add(e.State);

            svc.RegeneratePin();
            svc.RegeneratePin();

            // Wait a short time — no Expired should fire because the
            // 5-minute lifetime has not elapsed. The first PIN's
            // expiry continuation is cancelled by the second
            // RegeneratePin.
            await Task.Delay(500);

            Assert.Equal(2, states.Count); // Generated, Generated
            Assert.DoesNotContain(PairingState.Expired, states);
        }

        // ── Concurrent access ───────────────────────────────────

        [Fact]
        public async Task ConcurrentValidate_PinIsSingleUse_UnderConcurrency()
        {
            var svc = CreateService();
            svc.RegeneratePin();
            string pin = svc.GetCurrentPin();

            // Fire 20 concurrent ValidateAndExchangePin calls with
            // the correct PIN. Only one should succeed — the PIN is
            // single-use.
            var tasks = new Task<string?>[20];
            for (int i = 0; i < 20; i++)
            {
                tasks[i] = Task.Run(() => svc.ValidateAndExchangePin(pin));
            }
            var results = await Task.WhenAll(tasks);

            int successCount = results.Count(r => r != null);
            Assert.Equal(1, successCount);
        }

        [Fact]
        public async Task ConcurrentValidate_WrongPin_LocksAfterFive()
        {
            var svc = CreateService();
            svc.RegeneratePin();

            // Fire 20 concurrent wrong-PIN calls. The failed-attempt
            // counter must not lose increments — lock must fire
            // exactly once (on the 5th failure), and the service
            // must be inactive afterwards.
            var tasks = new Task[20];
            for (int i = 0; i < 20; i++)
            {
                tasks[i] = Task.Run(() => svc.ValidateAndExchangePin("000000"));
            }
            await Task.WhenAll(tasks);

            Assert.False(svc.IsPairingActive);
        }
    }
}
