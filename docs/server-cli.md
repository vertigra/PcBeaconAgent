# Server.Cli — Console Host

`PcBeaconAgent.Server.Cli` is the headless console host. It runs the
Web API + SignalR hub + UDP beacon in a single process, prints the
pairing PIN to stdout and the log file, and is suitable for running
as a Windows Service, from Task Scheduler, or interactively in a
terminal window.

The tray host ([Server.Tray](server-tray.md)) is a separate executable
that shares the same `Server.Core` business logic but adds a WPF UI
with a tray icon and PIN popup. The two hosts are mutually exclusive —
they bind the same UDP discovery port and the same HTTP port, so a
second instance of either will fail on socket bind. A single-instance
mutex to make the error message friendlier is tracked in the
[roadmap](roadmap.md).

## When to use which

| Use Server.Cli when… | Use Server.Tray when… |
|---|---|
| Running as a Windows Service (no UI session) | Running interactively on the user's desktop |
| Headless / scripted / `--silent` deployments | You want the PIN popup and tray icon |
| Debugging — full stdout logging | Auto-start on user login is desired |

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

### Windows Service mode

The executable is registered with `AddWindowsService` so it can run as
a Windows Service out of the box. Install with `sc.exe`:

```powershell
sc.exe create PcBeaconAgent binPath= "C:\path\to\PcBeaconAgent.Server.Cli.exe" start= auto
sc.exe start PcBeaconAgent
```

The service runs under the LocalSystem account by default. If you
need access to per-user audio devices, run the service under a
specific user account instead.

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

The CI pipeline (`publish-server.yml`) builds this automatically on
every `server.v.X.Y.Z` tag push. See the
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
- **No auto-start.** Use Windows Service or Task Scheduler.
- **No PIN state-change notifications.** The `PairingStateChanged`
  event is raised by `PairingService` but no subscriber in `Server.Cli`
  consumes it — the host has no UI surface to update. The log entries
  for each transition are still written.
