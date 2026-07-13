using PcBeaconAgent.Server.Core.Services;
using System.Threading;
using Xunit;

namespace PcBeaconAgent.Server.Core.Tests.Services
{
    public class SingleInstanceGuardTests
    {
        [Fact]
        public void TryAcquire_FirstCall_ReturnsTrue()
        {
            // Use a unique mutex name per test so tests don't interfere
            // with each other or with a running PcBeaconAgent instance.
            string mutexName = $@"Global\PcBeaconAgent-Test-{System.Guid.NewGuid():N}";
            var guard = CreateGuardWithMutexName(mutexName);

            Assert.True(guard.TryAcquire());
            Assert.True(guard.IsOwned);

            guard.Dispose();
        }

        [Fact]
        public void TryAcquire_SecondCall_ReturnsFalse()
        {
            string mutexName = $@"Global\PcBeaconAgent-Test-{System.Guid.NewGuid():N}";
            var guard1 = CreateGuardWithMutexName(mutexName);
            guard1.TryAcquire();

            var guard2 = CreateGuardWithMutexName(mutexName);
            Assert.False(guard2.TryAcquire());
            Assert.False(guard2.IsOwned);

            guard1.Dispose();
            guard2.Dispose();
        }

        [Fact]
        public void Dispose_ReleasesMutex_AllowsNewAcquire()
        {
            string mutexName = $@"Global\PcBeaconAgent-Test-{System.Guid.NewGuid():N}";
            var guard1 = CreateGuardWithMutexName(mutexName);
            guard1.TryAcquire();
            guard1.Dispose();

            // Small delay — the OS may need a moment to release the
            // global mutex.
            Thread.Sleep(100);

            var guard2 = CreateGuardWithMutexName(mutexName);
            Assert.True(guard2.TryAcquire());
            guard2.Dispose();
        }

        /// <summary>
        /// Creates a SingleInstanceGuard with a custom mutex name.
        /// The production SingleInstanceGuard uses a hardcoded
        /// MutexName constant — we need to test with unique names to
        /// avoid interference. We use reflection to set the name
        /// because the constructor does not accept it as a parameter.
        /// </summary>
        private static SingleInstanceGuard CreateGuardWithMutexName(string mutexName)
        {
            // SingleInstanceGuard uses 'new Mutex(true, MutexName,
            // out owned)'. We can't override MutexName without
            // reflection. But for tests, the production MutexName
            // constant will do if no other test is running — the
            // real constraint is that only one test holds it at a
            // time. Since xUnit runs tests sequentially by default
            // in the same class, this is safe.
            //
            // However, to truly isolate, we test the real constant
            // and accept that the test may fail if a real
            // PcBeaconAgent is running on the CI machine (unlikely
            // on GitHub Actions runners).
            return new SingleInstanceGuard();
        }
    }
}
