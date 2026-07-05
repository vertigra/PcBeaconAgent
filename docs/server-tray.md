# Server.Tray — WPF Tray Host

`PcBeaconAgent.Server.Tray` is the desktop counterpart to
[Server.Cli](server-cli.md). It runs the same Web API + SignalR hub +
UDP beacon, but adds a WPF UI on top:

- A system tray icon (`Hardcodet.NotifyIcon.Wpf`) with a context menu.
- A persistent PIN popup with a live countdown, snapped to the
  taskbar edge.
- Transient balloon notifications for terminal pairing states
  (Used / Expired / Locked).
- A `MainWindow` for explicit PIN management (regenerate, view).

Both hosts share the same `Server.Core` business logic, so behaviour
does not drift. The two are mutually exclusive — they bind the same
ports, so a second instance of either will fail on socket bind. A
single-instance mutex to make the error message friendlier is tracked
in the [roadmap](roadmap.md).

## When to use which

See the comparison table in [Server.Cli](server-cli.md#when-to-use-which).
Short version: use Tray for interactive desktop sessions, Cli for
headless / service / scripted deployments.

## Tray icon behaviour

| Action | Effect |
|---|---|
| **Single left-click** | If a PIN is active, opens the popup. If no PIN is active, opens `MainWindow`. |
| **Right-click** | Opens the context menu. |
| **Context menu → Show PIN** | Same as single left-click. |
| **Context menu → Regenerate PIN** | Generates a fresh PIN and opens the popup. |
| **Context menu → Exit** | Shuts the host down. |

## PIN lifecycle notifications

The host subscribes to `IPairingService.PairingStateChanged` and reacts
to each transition:

| State | Popup | Balloon | Tray tooltip |
|---|---|---|---|
| `Generated` | Opens (with PIN + countdown) | — | "PIN active" |
| `Generated` while `MainWindow` is open | Skipped (avoid duplicate UI) | — | "PIN active" |
| `Used` | Closes | Info: "A client has paired with this PC." | "Paired" |
| `Expired` | Closes | Warning: "The pairing PIN was not used and has expired." | "No active PIN" |
| `Locked` | Closes | Error: "Too many failed attempts. Restart the service to reset." | "Locked" |

The popup is the persistent display — it stays open for the entire
validity window (5 minutes by default) so the user can read the PIN
while switching to the phone. Balloons are short transient signals
(Windows clamps the on-screen duration to ~15 seconds — we cannot
control this). Custom balloon positioning is tracked in the
[roadmap](roadmap.md).

## PIN popup details

- **Positioning.** The popup snaps to the taskbar edge via
  `SHAppBarMessage(ABM_GETTASKBARPOS)`, supporting bottom / top /
  left / right taskbar docks. Falls back to `SystemParameters.WorkArea`
  on alternative shells.
- **Countdown.** A `DispatcherTimer` updates a `ProgressBar` and a
  `m:ss` text every 250 ms. The bar tints red in the final 30 seconds.
- **Topmost.** The popup stays above other windows so the PIN is
  visible while the user switches to the phone.
- **Drag-move.** The whole popup is draggable — click anywhere and
  drag to move it out of the way.
- **Close.** The `×` button, dragging beyond the screen, the countdown
  reaching zero, or any terminal pairing state (Used / Expired /
  Locked) all close the popup.
- **No "Copy PIN" button.** The PIN is 6 digits and is meant to be
  typed on a phone keyboard — copying it to the clipboard would not
  help (you'd still have to paste it into the Android client, which
  requires the Android clipboard, not the PC one). The popup is the
  display surface; the phone is the input surface.

## Configuration

`Server.Tray` uses the same `appsettings.json` schema as `Server.Cli`.
See [Server.Cli configuration](server-cli.md#configuration).

## Auto-start

Auto-start on user login is **not yet implemented**. It will be added
via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (no admin
rights needed) — see the [roadmap](roadmap.md).

## Build & publish

```bash
dotnet publish src/PcBeaconAgent.Server.Tray/PcBeaconAgent.Server.Tray.csproj \
  -c Release \
  -r win-x64
```

CI/CD integration for the tray host is tracked in the
[roadmap](roadmap.md) — the plan is for `publish-server.yml` to build
both hosts and ship them together under the same `server.v.X.Y.Z` tag.

## What's not in Server.Tray

- **No console window.** Output goes to the log file only.
- **No Windows Service support.** Use [Server.Cli](server-cli.md) for
  service-mode deployments.
- **No `--silent` flag.** The host is silent by design — the UI *is*
  the notification surface.
