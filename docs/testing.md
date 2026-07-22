# Testing

## Project structure

```
src/PcBeaconAgent.Server.Core.Tests/
  PcBeaconAgent.Server.Core.Tests.csproj
  Services/
    ConnectionTrackerTests.cs
    PairingServiceTests.cs
    BeaconServerIdentityTests.cs
    SingleInstanceGuardTests.cs
    BeaconServerTests.cs

src/PcBeaconAgent.Client.Core.Tests/
  PcBeaconAgent.Client.Core.Tests.csproj
  Stores/
    DeviceStoreTests.cs
  Helpers/
    UrlHelpersTests.cs
  Models/
    ManagedDeviceTests.cs
```

## Framework

- **xUnit** — test runner
- **Moq** — mocking (ILogger<T>, IBeaconServerIdentity)
- **coverlet** — code coverage collection

## Running tests

```bash
# Run all tests
dotnet test src/PcBeaconAgent.Server.Core.Tests/PcBeaconAgent.Server.Core.Tests.csproj

# Run with coverage
dotnet test src/PcBeaconAgent.Server.Core.Tests/PcBeaconAgent.Server.Core.Tests.csproj --collect:"XPlat Code Coverage"
```

## InternalsVisibleTo

`Server.Core` exposes internals to the test project via
`[assembly: InternalsVisibleTo]` in the csproj. This lets tests see
`internal` types like `PairingService`, `ConnectionTracker`, and
`BeaconServerIdentity`.

Additionally, `InternalsVisibleTo("DynamicProxyGenAssembly2")` allows
Moq to create proxies for `ILogger<T>` where `T` is an internal type.

## What is tested

| Class | Coverage |
|-------|----------|
| `PairingService` | PIN lifecycle (Generated/Used/Expired), single-use, lockout (5 attempts), wrong PIN, empty/null PIN, event sequence, regenerate after used/lock, concurrent access (20 parallel) |
| `ConnectionTracker` | Register/unregister count, client info storage, duplicate overwrite, unknown ID no-op, snapshot isolation, CountChanged event, 50-concurrent register, 50-concurrent unregister |
| `BeaconServerIdentity` | Static key from config, load from file, generate new, 32-char hex format, trim whitespace, persistence across instances |
| `SingleInstanceGuard` | First acquire succeeds, second acquire fails, dispose releases |
| `BeaconServer` | UDP ping/pong integration test (loopback, random port) |
| `TransferController` | Basic receive, validation (empty/whitespace/size cap), history ordering, history cap eviction, event raising outside lock, source IP normalisation, concurrency (50 parallel, cap+20 overflow) |

### Client.Core.Tests

| Class | Coverage |
|-------|----------|
| `DeviceStore` | Add/forget device, duplicate prevention, different IP, persistence (SaveDevices called), load from storage, duplicate load dedup |
| `UrlHelpers` | BuildUrl with various IP/port/path combinations |
| `ManagedDevice` | Equals (same, different, null, self-ref), GetHashCode, IsOnline default + PropertyChanged |

## What is NOT tested

| Class | Reason |
|-------|--------|
| `DisplayController` | Uses `WindowsDisplayAPI` (CCD) — requires a real Windows desktop session with monitors |
| `AudioController` | Uses `AudioSwitcher.AudioApi.CoreAudio` (WASAPI) — requires real audio devices |
| `BeaconServiceHub` | SignalR hub — requires a full ASP.NET Core host. Integration-tested manually. |
| `Endpoints/*Extensions` | ASP.NET Core endpoint routing — requires a full host. Tested via smoke test in CI. |

## CI

Tests run on:
- Every push to `devel` branch (`tests.yml`)
- Every pull request to `master` (`tests.yml`)
- Before every release (`publish-all.yml` → `test` job runs first,
  `build-server` and `build-client` jobs run in parallel only if `test`
  passes)

Coverage is collected in CI via `--collect:"XPlat Code Coverage"` and
can be downloaded as an artifact from the test run.
