# Server.Cli — Console Host

`PcBeaconAgent.Server.Cli` is the interactive console host. It runs the
Web API + SignalR hub + UDP beacon in a single process, prints the
pairing PIN to stdout and the log file, and is meant to be run in a
terminal window you keep open while using the agent.

The tray host ([Server.Tray](server-tray.md)) is a separate executable
that shares the same `Server.Core` business logic but adds a WPF UI
with a tray icon and PIN popup. The two hosts are mutually exclusive —
they share a single-instance mutex (`Global\PcBeaconAgent-SingleInstance`)
so a second instance of either exits cleanly with a clear message
instead of crashing on `AddressAlreadyInUse`.

## When to use which

| Use Server.Cli when… | Use Server.Tray when… |
|---|---|
| Debugging — full stdout logging in a terminal | Running interactively on the user's desktop |
| Scripted / short-lived use from a terminal | You want the PIN popup and tray icon |
| You want to read PIN transitions in real time | Auto-start on user login is desired |

> ⚠️ **Server.Cli is interactive-only.** It does not register as a
> Windows Service and does not support headless / Session 0 deployments
> — `DisplayController` and `AudioController` require an interactive
> desktop session, so a Session 0 service would have no monitors and
> no audio devices to control. For always-on use, run `Server.Tray`
> and configure auto-start (planned, see [roadmap](roadmap.md)).

## CLI arguments

### Silent mode

By default, the agent duplicates all logs to the console window. For
background or scripted execution, you can suppress terminal output:

```
# Run in silent mode (logs go to the file only):
PcBeaconAgent.Server.Cli.exe --no-console

# Alias:
PcBeaconAgent.Server.Cli.exe --silent
```

## Configuration

All runtime configuration lives in `appsettings.json` next to the
executable:

```json
{
  "Server": {
    "Host": "0.0.0.0",
    "DiscoveryPort": 5001,
    "ApiPort": 5000,
    "ApiKey": ""
  },
  "Logging": {
    "Path": "logs/pcbeacon.log"
  }
}
```

- **`Server.Host`** — bind address for the HTTP API. `0.0.0.0` listens
  on all interfaces (LAN-reachable); `127.0.0.1` restricts to localhost.
- **`Server.DiscoveryPort`** — UDP port for the beacon broadcast.
- **`Server.ApiPort`** — HTTP port for the Web API + SignalR hub.
- **`Server.ApiKey`** — if empty, the server generates a random key on
  first run and persists it to `server.key`. If non-empty, the
  configured key is used (useful for pre-provisioned deployments).
- **`Logging.Path`** — log file path (Serilog file sink).

## Build & publish

The project is configured for single-file self-contained publish:

```bash
dotnet publish src/PcBeaconAgent.Server.Cli/PcBeaconAgent.Server.Cli.csproj \
  -c Release \
  -r win-x64
```

The CI pipeline (`publish-all.yml`) builds this automatically on
every `release.v.X.Y.Z` tag push. See the
[main README](../README.md#internal-version-processing-mechanics) for
the release workflow.

## Logs

Logs are written to the file specified in `appsettings.json` via
Serilog. The PIN is logged at `Warning` level with prominent separators
so it's easy to find in a scrolling log:

```
══════════════════════════════════════
  PAIRING PIN : 421849
  Valid for   : 5 minutes
══════════════════════════════════════
```

In interactive mode the same banner is printed to stdout. In
`--silent` mode it goes only to the file.

## What's not in Server.Cli

- **No tray icon, no popup, no balloons.** The PIN is in the log only.
  If you need a UI, use [Server.Tray](server-tray.md).
- **No auto-start.** Launch the executable manually when you need it.
- **No Windows Service support.** The controllers require an
  interactive desktop session.
- **No PIN state-change notifications.** The `PairingStateChanged`
  event is raised by `PairingService` but no subscriber in `Server.Cli`
  consumes it — the host has no UI surface to update. The log entries
  for each transition are still written.
