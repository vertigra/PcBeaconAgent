# Coding Guidelines

This document captures the conventions already established in the
PcBeaconAgent codebase. It is descriptive, not prescriptive — when in
doubt, match the existing code. New conventions are introduced only
when they reduce ambiguity or prevent recurring bugs.

## 1. Naming conventions

### 1.1 Namespaces

- Match the folder path: file at `Services/BeaconServer.cs` declares
  `namespace PcBeaconAgent.Client.Core.Services`.
- One namespace per folder; do not split a folder across multiple namespaces.

### 1.2 Identifiers

- **Types, public members, methods:** `PascalCase`.
  - `BeaconServer`, `StartAsync`, `OnResponseSent`.
- **Private fields:** `m` prefix + `PascalCase`.
  - `mLogger`, `mBeaconServerOptions`, `mConnections`.
- **Locals and parameters:** `camelCase`.
  - `stoppingToken`, `result`, `portBytes`.
- **Constants:** `PascalCase` for public, `camelCase` or `PascalCase` for
  private — match the surrounding file. Existing code uses both; do not
  reformat untouched code.
- **Event names:** verb in present tense, `On` prefix optional.
  - `OnBeaconFound`, `OnResponseSent`, `DeviceStatusChanged`.

### 1.3 Files

- One public type per file. The file name matches the type name.
- Small related types (DTOs, records) may share a file if they form a
  cohesive group (e.g. `PairingDtos.cs` contains `PairRequestDto` +
  `PairResponseDto`).

## 2. Logging

All logging in production code paths MUST go through `LoggerMessage.Define`.
Direct `ILogger.Log*` calls are forbidden in service classes — they allocate
strings on every call and break structured log filtering.

### 2.1 Pattern

Each class that logs declares:

1. A `private static readonly Action<ILogger, ...>` field per log event.
2. A `private void LogXxx(...)` instance method that invokes the delegate.
3. All declarations grouped inside
   `#region Structured logging definitions (allocation-free)`.

Region is the standard — every class that uses `LoggerMessage.Define` MUST
wrap its definitions in this region, placed at the end of the class.

```csharp
public class BeaconServer : IBeaconServer
{
    // ... fields, ctor, business methods ...

    #region Structured logging definitions (allocation-free)

    private static readonly Action<ILogger, int, IPEndPoint, Exception?> LogPortSentAction =
        LoggerMessage.Define<int, IPEndPoint>(
            LogLevel.Information,
            new EventId(40, "PortSent"),
            "Sent API port {Port} to {EndPoint}");

    private static readonly Action<ILogger, Exception?> LogShuttingDownAction =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(41, "ShuttingDown"),
            "UDP Beacon Server is shutting down...");

    private void LogPortSent(int port, IPEndPoint endPoint) =>
        LogPortSentAction(mLogger, port, endPoint, null);

    private void LogShuttingDown() =>
        LogShuttingDownAction(mLogger, null);

    #endregion
}
```

### 2.2 EventId allocation

Each class owns a contiguous EventId range. Before adding a new event,
check existing ranges and pick the next free one:

| Class                              | Range      |
| ---------------------------------- | ---------- |
| `BeaconServiceHub`                 | 1 – 3      |
| `PairingService`                   | 10 – 14    |
| `BeaconServerIdentity`             | 20 – 22    |
| `MauiPreferencesService`           | 30 – 33    |
| `BeaconServer`                     | 40 – 42    |
| `DisplayController`                | 100 – 105  |
| `AudioController`                  | 110 – 114  |

When adding a new class, claim the next free decade (50, 60, 70, ...) and
register it in this table.

### 2.3 Message templates

- Use **named placeholders** in curly braces: `{Port}`, `{EndPoint}`,
  `{Remaining}`. Never use string interpolation in the template — the
  structured logger will not be able to capture the values.
- End informational messages with no punctuation; end error messages with a
  period. Match the style of the nearest existing log message.
- The last parameter of `LoggerMessage.Define` is always `Exception?`. Pass
  `null` for non-error events, pass the caught exception for error events.

## 3. Dependency Injection

### 3.1 Lifetimes

| Lifetime     | When to use                                                                |
| ------------ | -------------------------------------------------------------------------- |
| `Singleton`  | Stateless services, thread-safe services, hosts (`BeaconServer`, `PairingService`, `DisplayController`) |
| `Scoped`     | Per-request state (rare in this project; endpoints receive singletons)    |
| `Transient`  | Lightweight, short-lived objects (Pages, ViewModels in MAUI)              |

### 3.2 Registration style

- Group related registrations in an extension method on `IServiceCollection`:
  `AddSignal()`, `AddAudioService()`, `AddDisplayService()`, `AddPairingService()`.
- Each extension lives in an `Extensions/<Area>Extensions.cs` file (Service)
  or in `MauiProgram.cs` (Android client).
- Avoid registering the same service from two different extensions.

### 3.3 Options

- Configuration is bound to record types (e.g. `BeaconServerOptions`,
  `WebApiOptions`) that live in the shared `Core` project.
- Records are registered as singletons in the host's composition root.
- Services receive options via constructor injection — never read
  `AppSettings` directly outside of the composition root.

### 3.4 Assembly boundaries and visibility

The solution is a monorepo: `PcBeaconAgent.Client.Core` is shared by both
the Service host and the Android client. We do not split Core into separate
client/server contract libraries — the cost of duplicate contracts and
extra projects outweighs the benefit of compile-time isolation.

Consequences:

- **`public` vs `internal`** controls visibility across assembly boundaries,
  not across projects within a solution. A `public` class in Core is
  reachable from any project that references Core.
- **`internal` + an extension method** (e.g. `AddBeaconServer()`) is used
  to discourage direct instantiation of server-only implementation classes
  from client code. This is a convention, not a hard guarantee — a
  determined caller can still invoke the public extension.
- **Server-only classes should not be registered in the client's DI
  container.** This is enforced by code review and convention, not by the
  compiler. If you find yourself wanting `InternalsVisibleTo` or a
  separate server-only assembly, raise the question in a PR first — the
  default answer is "stay in Core."

* DI-activated constructors must be public. Microsoft.Extensions.DependencyInjectionresolves 
  constructors via reflection with public binding flags. A classmarked internal can still have 
  a public constructor — the type staysinvisible outside its assembly, but the DI container can i
  nstantiate it.Marking the constructor itself internal (or any non-public modifier)makes the service 
  unresolvable and crashes the host at startup with"A suitable constructor could not be located". 
  This is the rule, not arecommendation: if a class is registered in DI through AddSingleton, AddScoped, 
  AddTransient, or AddHostedService, its constructorMUST be public.

## 4. Async and cancellation

- All async methods return `Task` or `Task<T>`. Avoid `async void` except
  for event handlers.
- Public async methods accept a `CancellationToken` parameter named `ct`
  (default value `default`) when they are RPC-style, or `stoppingToken`
  when they are background-service-style.
- Pass the token to every `await` that accepts one.
- For linked cancellation (e.g. apply a timeout on top of a caller's token),
  use `CancellationTokenSource.CreateLinkedTokenSource` and dispose it via
  `using`.

## 5. JSON serialization

- All types that travel over SignalR or HTTP MUST be registered in
  `ProjectJsonContext` (`Client.Core/JsonContext/ProjectJsonContext.cs`).
- The context is added to both `System.Text.Json` (via
  `ConfigureHttpJsonOptions`) and SignalR (via `AddJsonProtocol`) in
  `SignalExtension.AddSignal`.
- DTOs are `record` types when possible; use `record` for immutable payloads,
  `class` only when inheritance or mutability is required.

## 6. Exceptions

- Throw domain exceptions (`NotPairedException`) for expected business
  failures. Catch them at the boundary (ViewModel, endpoint) and convert to
  user-facing messages.
- Do not catch `Exception` to swallow it silently. Log via the structured
  logger and rethrow, or convert to a domain exception with the original as
  `InnerException`.
- Endpoint error responses use
  `Results.Json(new MessageDto(ex.Message), ...)` with the appropriate
  status code. Never leak stack traces to the client.

## 7. Source control

### 7.1 Commit messages

Follow [Conventional Commits](https://www.conventionalcommits.org/) as
described in [CONTRIBUTING.md](../CONTRIBUTING.md). Recap:

```
<type>(<scope>): <short summary>

- bullet point 1
- bullet point 2
```

- Types: `feat`, `fix`, `docs`, `refactor`, `ci`, `test`.
- Scopes: `client`, `server`, `core`.
- Summary in imperative mood, lowercase, no trailing period.
- Body: optional, bullets with `-`. Explain *why*, not just *what* — the
  diff already shows *what*.

### 7.2 Atomic commits

- One commit = one logical change. Moving a type from Service to Core and
  changing its logging style are two commits.
- The project must compile and all tests must pass at every commit.
- Avoid mixing formatting churn (renames, whitespace) with semantic changes.

## 8. XAML (MAUI)

- Pages live in a `Pages/` folder. ViewModels live in a `ViewModels/` folder.
  The folder pair mirrors each other — `AudioControlPage.xaml` ↔
  `AudioControlViewModel.cs`.
- Use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`,
  `[RelayCommand]`) instead of hand-written `INotifyPropertyChanged`.
- Bindings reference the VM by name; do not put presentation logic in
  code-behind. Code-behind contains only lifecycle hooks and UI-only helpers.

## 9. C# language features

- **Target framework:** `net10.0` for all projects.
- **Nullable reference types:** enabled (`<Nullable>enable</Nullable>`).
  Honor nullability in signatures; do not suppress with `!` except in
  initialization helpers.
- **`ImplicitUsings`:** disabled. Add `using` directives explicitly.
- **Primary constructors:** preferred for classes with simple DI needs
  (`BeaconServiceHub(IBeaconServerIdentity svc, ILogger<BeaconHub> log)`).
  Use the `m` prefix when promoting a primary-constructor parameter to a
  field by capturing it in a method.
- **Collection expressions:** use `[...]` for newly-allocated collections
  (`byte[] response = [pong, portBytes[0], portBytes[1]];`).
- **`var`:** use when the type is obvious from the right-hand side. Use the
  explicit type when it adds readability (`BeaconDevice details = ...`).
