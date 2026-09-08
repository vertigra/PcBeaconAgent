# Server.Tray — WPF Tray Host

`PcBeaconAgent.Server.Tray` is the desktop counterpart to
[Server.Cli](server-cli.md). It runs the same Web API + SignalR hub +
UDP beacon, but adds a WPF UI on top:

- A system tray icon (`Hardcodet.NotifyIcon.Wpf`) with a context menu.
- A persistent PIN popup with a live countdown, snapped to the
  taskbar edge.
- A transient toast (custom WPF popup) for terminal pairing states
  (Used / Expired / Locked), pixel-aligned with the PIN popup.
- A `MainWindow` for explicit PIN management (regenerate, view).

Both hosts share the same `Server.Core` business logic, so behaviour
does not drift. The two are mutually exclusive — they bind the same
ports, so a second instance of either will fail on socket bind. A
single-instance mutex (`Global\PcBeaconAgent-SingleInstance`) makes
the error message friendlier: the second process logs a clear error
and exits instead of crashing on `AddressAlreadyInUse`.

## When to use which

See the comparison table in [Server.Cli](server-cli.md#when-to-use-which).
Short version: use Tray for interactive desktop sessions, Cli for
headless / service / scripted deployments.

## Main window

The main window is the interactive hub. Open it with a single left-click
on the tray icon (or the "Show window" context menu item). It has three
tabs:

- **Pairing** — current PIN display + Regenerate button. Same content
  the popup shows, but in a permanent window you can keep open.
- **Settings** — auto-start toggle (Startup section) + app name and
  version (About section). Future settings (network, security, updates,
  log path) will be added as additional expanders.
- **Files** — incoming text transfer history and auto-copy settings.
  Shows a list of received transfers (newest first) with timestamp,
  source IP, and a Copy button per item. A Settings expander toggles
  auto-copy-to-clipboard (default on) and offers a Clear history
  button. Populated by `TransferController.TransferReceived` events
  via `FilesViewModel`.

The window header shows the app icon, name, and version. The window is
not modal — it stays open until the user closes it, and the tray host
keeps running. Closing the window does not exit the application; use
the tray context menu → Exit for that.

## Tray icon behaviour

| Action | Effect |
|---|---|
| **Single left-click** | Opens `MainWindow`. The popup is a passive notification surface driven by the Generated event, not by user clicks. |
| **Right-click** | Opens the context menu. |
| **Context menu → Show window** | Same as single left-click. |
| **Context menu → Exit** | Shuts the host down. |

## PIN lifecycle notifications

The host subscribes to `IPairingService.PairingStateChanged` and reacts
to each transition:

| State | Popup | Toast | Tray tooltip |
|---|---|---|---|
| `Generated` | Opens (with PIN + countdown) | — | "PIN active" |
| `Generated` while `MainWindow` is open | Skipped (avoid duplicate UI) | — | "PIN active" |
| `Used` | Closes | Info: "A client has paired with this PC." | "Paired" |
| `Expired` | Closes | Warning: "The pairing PIN was not used and has expired." | "No active PIN" |
| `Locked` | Closes | Error: "Too many failed attempts. Restart the service to reset." | "Locked" |

The popup is the persistent display — it stays open for the entire
validity window (5 minutes by default) so the user can read the PIN
while switching to the phone. The toast is a short transient signal
(5 seconds, click-to-dismiss), positioned next to the taskbar via the
same `SHAppBarMessage`-based logic as the popup so the two surfaces
stay pixel-aligned.

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

Auto-start on user login is implemented via the per-user
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` registry key — no
admin rights needed. The feature is **off by default**; the user opts
in via the Settings tab in the main window.

- **Enable:** open the main window (tray single-click → "Show window"),
  go to the Settings tab, expand the Startup section, tick "Start
  PcBeaconAgent when I log in".
- **Disable:** uncheck the same box.
- **Registry value name:** `PcBeaconAgent.Server.Tray` under the `Run`
  key above. The value is the full path to the executable, quoted so
  paths with spaces survive.
- **If the registry write fails** (group policy, locked-down hive), the
  checkbox reverts silently — the rest of the app still works.

## Build & publish

```bash
dotnet publish src/PcBeaconAgent.Server.Tray/PcBeaconAgent.Server.Tray.csproj \
  -c Release \
  -r win-x64
```

CI/CD: `publish-all.yml` builds and publishes both `Server.Cli` and
`Server.Tray` (plus the Android client) under the same `release.v.X.Y.Z`
tag. `Server.Tray` is published with `PublishTrimmed=false` (WPF XAML
bindings trim unreliably) while `Server.Cli` keeps `PublishTrimmed=true`.
Smoke test covers `Server.Cli` only — `Server.Tray` is a GUI app that
needs a desktop session and cannot be easily smoke-tested in CI.

## What's not in Server.Tray

- **No console window.** Output goes to the log file only.
- **No `--silent` flag.** The host is silent by design — the UI *is*
  the notification surface.
