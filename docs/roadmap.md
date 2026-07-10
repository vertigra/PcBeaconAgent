# Roadmap

This file tracks planned features, architectural changes, and known
follow-up items that are out of scope of the current iteration. It is
a living document — items move up when they become the next focus and
get a commit reference when they ship.

Items are grouped by priority tier, not by category. Within a tier the
order is loose.

## Tier 1 — polish the existing surface ✅ DONE

All Tier 1 items shipped. The existing functionality (audio, display,
pairing, discovery) is now solid: primary display is handled correctly,
PIN generation is cryptographically secure, the pairing critical
section is locked, and the client HTTP layer is factored out into
service clients.

- [x] **Display: disable the primary display correctly.** (`facff1b`)
      When the disabled display was the GDI primary, the first surviving
      display is rebuilt as a new `PathInfo` with `Position = Point.Empty`
      so `ApplyPathInfos` accepts the path set.
- [x] **Display: last-active-display guard.** (`0809f7f`)
      `DisableByDevicePath` throws a clear error before calling
      `ApplyPathInfos` when the remaining path set is empty. The client
      blocks the Disable button via `IsLastActive` and shows the server's
      explanation if the check is bypassed.
- [x] **Refactor `PairingViewModel` → `PairingServiceClient`.** (`362fdf0`)
      `PairingViewModel` no longer touches `HttpClient` directly; all
      pairing HTTP calls go through `IPairingServiceClient` (+
      `PairingServiceClient`, `PairingHttpException`).
- [x] **`RandomNumberGenerator` for the PIN.** (`618ede8`)
      `PairingService.GeneratePin` uses `RandomNumberGenerator.GetInt32`
      (CSPRNG) instead of `Random.Shared`.
- [x] **`lock` in `PairingService`.** (`3ea94aa`)
      `ValidateAndExchangePin` and `RegeneratePin` are wrapped in
      `lock(mStateLock)` — concurrent `/api/pair` calls can no longer
      both succeed or lose the failed-attempt counter.
- [x] **Display: mark the primary display in the mobile client.** (`a9190ae`)
      `DisplayDeviceDto` carries `IsPrimary`; the server computes it via
      `PathInfo.IsGDIPrimary`; the client shows a gold "★ Primary" badge.
- [x] **Refactor: extract `DisplayInfo` into its own file.** (`edaa47e`)
      `DisplayDeviceItem` moved out of `DisplayControlViewModel.cs` and
      renamed to `DisplayInfo`.

### Tier 1 carry-over (non-blocking, deferred to a future pass)

These items were identified during Tier 1 work but are not blocking the
tray host. They stay in the backlog:

- [x] **Discovery: do not block found devices while scanning.**
      `DiscoveryPage` previously showed a full-area overlay while
      `IsScanning` was true, blocking the user from tapping "Remember"
      on already-discovered devices. Fixed: replaced the overlay with a
      thin scanning banner at the top of the page (ActivityIndicator +
      "Scanning for devices..." label) that does not cover the list. The
      device list stays interactive throughout the scan.
- [x] **Audio: empty device list on first request after server start.**
      `CoreAudioController` (COM) may need a moment to enumerate
      playback devices after the server process starts. Fixed: the
      controller is now created lazily on the first call (not in the
      constructor), and `GetDevices` retries the enumeration up to 5
      times with 500ms delay before returning. This gives WASAPI time
      to finish initialising the device collection.
- [x] **Managed device: disable control buttons when offline.** (`9c9c130`)
      The Audio and Display buttons on a `ManagedDevice` card are now
      disabled (`IsEnabled=false`) when the device is offline, with a
      `VisualStateManager` `Disabled` state (Opacity 0.3, grey background)
      for clear visual feedback. The Forget button stays enabled —
      forgetting a device only removes the local pairing key and does not
      require a live connection.
- [ ] **Display: improve topology UI.**
      The current topology indicator is a plain text label ("Topology:
      Extend"). Consider replacing it with monitor icons (e.g. two
      overlapping rectangles for Clone, side-by-side for Extend) for
      better visual clarity.

## Tier 2 — tray host

The `PcBeaconAgent.Server.Cli` console host stays. A new
`PcBeaconAgent.Server.Tray` project is added alongside it:

- WPF + `Hardcodet.NotifyIcon.Wpf` (pure WPF tray icon — no WinForms
  interop). Shipped.
- References `Server.Core` — reuses pairing, beacon, controllers, hub.
  Shipped.
- **PIN popup with countdown** (`PinPopupWindow` + `PinPopupViewModel`)
  snapped to the taskbar edge via `SHAppBarMessage`. Shipped.
- **Balloon notifications** for terminal pairing states (Used / Expired /
  Locked) via `INotificationService`. Shipped. Custom positioning of the
  balloons themselves is tracked in Tier 3 — Windows positions
  `Shell_NotifyIcon` balloons next to the tray icon and we cannot
  control it.
- [x] **Auto-start on user login.** (`0003cec`)
      Implemented via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
      (no admin rights needed). Off by default — the user opts in via
      the Settings tab in the main window. Registry value name is
      `PcBeaconAgent.Server.Tray`, value is the quoted full path to
      the executable. If the registry write fails (group policy,
      locked-down hive), the checkbox reverts silently.
- [x] **Single-instance mutex.** (`5f4ecbb`)
      A named system mutex (`Global\PcBeaconAgent-SingleInstance`) is
      acquired by both `Server.Cli` and `Server.Tray` at startup. If
      the mutex is already held — by the other host or by a duplicate
      of the same host — the new process logs a clear error and exits
      instead of crashing on `AddressAlreadyInUse` from the UDP
      discovery or HTTP port bind. The mutex is global (not
      session-local) so the constraint holds even if a future build
      runs one host in the interactive session and another under a
      different account. In `Server.Cli` the failure path prints a
      yellow banner to stderr and waits for Enter so the user can
      actually read why nothing opened (`e15feb1`); `Server.Tray`
      shows a `MessageBox` with the same explanation.
- Settings window: API key, ports, log path, auto-start toggle.
      **Partially shipped** — the main window has a Settings tab with
      Startup (auto-start toggle), Network (read-only host/ports),
      About (app name + version + update link), and Logs (log path +
      open folder) sections. Editing network settings requires a soft
      restart (Tier 3). API key management is still pending.
- The existing `Server.Cli` keeps working for debug / scripted /
  interactive scenarios. Both hosts share the same `Server.Core`
  business logic, so no behaviour drift.
- [x] **Drop Windows Service support from `Server.Cli`.** (`e15feb1`)
      The `AddWindowsService` registration, `EnableComHosting`,
      `BuiltInComInteropSupport`, and the
      `Microsoft.Extensions.Hosting.WindowsServices` package were
      removed. `DisplayController` (CCM/CCD) and `AudioController`
      (WASAPI) require an interactive desktop session — they have
      nothing to control in Session 0. The service-mode registration
      was misleading documentation; it never actually worked. The
      always-on use case is handled by `Server.Tray` with auto-start
      on user login (still pending).
- [x] **CI/CD for Server.Tray.** (`<TBD>`)
      The `publish-server.yml` workflow now builds and publishes both
      `Server.Cli` and `Server.Tray` under the same `server.v.X.Y.Z`
      tag. The release includes both ZIPs. `Server.Tray` is published
      with `PublishTrimmed=false` (WPF XAML bindings trim unreliably)
      while `Server.Cli` keeps `PublishTrimmed=true`. Smoke test
      covers `Server.Cli` only — `Server.Tray` is a GUI app that
      needs a desktop session and cannot be easily smoke-tested in CI.
- [x] **Documentation for Server.Tray.** (`e782f92`)
      Split into [docs/server-cli.md](server-cli.md) and
      [docs/server-tray.md](server-tray.md); `README.md` updated with a
      comparison table and links to both.
- [ ] **Status bar in MainWindow.**
      Add a status bar at the bottom of MainWindow (below the
      TabControl, common to all tabs) showing the connected-clients
      count. Requires `BeaconServiceHub` to expose a thread-safe
      `ConnectedClientsCount` property (or an event the VM
      subscribes to) — today the hub tracks connections internally
      but does not publish the count. The status bar should also
      show the server's IP:port so the user can confirm the bind
      address without opening Settings.
- [ ] **Add unit and integration tests.**
      The project currently has no test projects. Add a
      `PcBeaconAgent.Server.Core.Tests` project (xUnit) covering the
      pure-logic services: `PairingService` (PIN state machine, lockout,
      expiry, thread safety, the `PairingStateChanged` event sequence
      under regenerate/use/expire races), `BeaconServerIdentity` (key
      loading / generation). Add a `PcBeaconAgent.Client.Core.Tests`
      project covering `DeviceStore`, `DeviceFactory`, `SignalService`
      (mocked SignalR). Controllers that wrap Windows-only COM / CCD
      APIs (`AudioController`, `DisplayController`) are tested manually
      on a real machine — mark them with `[Trait("Category",
      "RequiresWindows")]` so CI can skip them. `INotificationService`
      in `Server.Tray` is testable by mocking the interface — verify
      that `TrayViewModel` routes each `PairingState` to the expected
      `ShowPinPopup` / `ClosePinPopup` / `ShowTransient` calls and
      suppresses them when `IsMainWindowVisible`.

## Tier 3 — new feature modules

Roughly ordered by expected value. Each item will get its own design
note before implementation; the entries here are reminders.

- [ ] **Virtual gamepad (ViGEmBus).**
      Android UI exposes D-pad + buttons; the server creates a virtual
      Xbox 360 controller via `Nefarius.ViGEm.Client`. Requires the
      ViGEmBus driver to be installed on the host (bundled in the
      installer). Target use case: navigating gamepad-friendly apps
      (Steam Big Picture, media centers, emulators).
- [ ] **Device info card with local cache.**
      Reduce the managed-device card to the essentials (machine name,
      IP, interface type) and move the rest into a detail page opened
      via an "Info" button. The detail page shows full hardware info
      (CPU, RAM, OS version, GPU, disk, etc.) fetched from the server.
      The info is cached locally so it is available even when the
      device is offline; when the device is online, the info is
      refreshed automatically. Requires a new server endpoint
      (`GET /api/system/info`) and a local cache in `ManagedDevice`.
- [ ] **Virtual keyboard / mouse (`SendInput`).**
      Two sub-modes: Unicode text entry (layout-independent, for typing
      into fields) and scancode hotkeys (layout-dependent, for macro
      keys like Ctrl+Shift+Esc). Same SignalR transport as the gamepad.
- [ ] **QR code for the pairing PIN.**
      Render the 6-digit PIN as a QR in the `Server.Cli` console
      (pseudo-graphics via `QRCoder`) and in the `Server.Tray` window
      (real bitmap). The Android client adds a "Scan QR" button on the
      PairingPage that opens the camera and auto-fills the PIN field.
      Removes the need to type the PIN manually.
- [ ] **Process management (read-only + kill own).**
      `GET /api/processes` lists the current user's processes.
      `POST /api/processes/{pid}/kill` terminates a process the server
      can access (no UAC, no cross-user kills). No arbitrary command
      execution. Covers the 80% "frozen app" use case without opening
      the remote-shell attack surface.
- [ ] **Server → client push events.**
      Today SignalR is RPC-only. Add `IStatePublisher` on the server
      and `Clients.Caller.SendAsync("StateChanged", ...)` for:
      audio device change, display hotplug, process start/stop (opt-in).
      The client subscribes and updates the UI in real time instead of
      polling on every page open.
- [ ] **Architectural unification — `IDeviceModule` / `ICommandHandler`.**
      Today audio, display, and (future) input each have their own
      endpoint mapper and their own `*ServiceClient` on the client.
      Introduce a single `IDeviceModule` registry on the server and a
      single `ExecuteAsync(module, command, payload)` SignalR method.
      New modules (gamepad, keyboard, processes) plug in without
      touching the hub or the endpoint layer.
- [ ] **Light / Dark theme in the MAUI client.**
      Trivial: `Application.Current!.UserAppTheme = AppInfo.RequestedTheme`.
      Add a manual override in Settings stored in `Preferences`.
- [ ] **i18n / multi-language support.**
      All UI strings in `Client.Android` and `Server.Tray` are currently
      hard-coded English (e.g. "Pairing PIN", "Show window", "Go to
      Discovery"). Switch to .NET resource files (`.resx`) per project
      with `IStringLocalizer`-style lookup, ship English + Russian as
      the first two locales, add a language picker in Settings. The
      server's PIN popup and balloon strings go through the same
      mechanism so the tray host inherits the locale from the OS or
      from a setting in `appsettings.json`. ClI host stays English-only
      (its output is log-shaped, not user-facing prose).
- [ ] **Custom balloon / toast positioning.**
      The Windows `Shell_NotifyIcon` balloon API positions the balloon
      next to the tray icon — the app cannot control it. The PIN popup
      (patch #39) snaps to the taskbar via `SHAppBarMessage`, but the
      transient terminal-state balloons (Used / Expired / Locked) still
      appear wherever Windows decides. Two options: (a) switch to the
      UWP Toast API (`Microsoft.Toolkit.Uwp.Notifications`) which gives
      a richer UI and lands in the Action Center but still doesn't let
      us pick the on-screen position; (b) replace balloons entirely
      with a small transient WPF popup (similar to `PinPopupWindow`
      but auto-closing after a few seconds) positioned next to the
      tray icon via `Shell_NotifyIconGetRect`. Option (b) is the only
      way to get pixel-perfect control. Defer until the rest of Tier 2
      ships; the current balloons are functional, just not pixel-aligned
      with the popup.
- [ ] **Network interface binding + soft restart.**
      Today the server binds to a single host address from
      `appsettings.json` (`0.0.0.0` = all interfaces). Add a
      Settings UI section that lists the machine's network
      interfaces (enumerated via `NetworkInterface.GetAllNetworkInterfaces`)
      and lets the user pick which one(s) to bind on. Changing the
      bind address or port does not require restarting the whole
      tray process — instead, implement a soft restart: stop the
      Kestrel host, reconfigure, start again. The tray UI stays
      alive throughout; only the web API + SignalR hub bounce.
      Clients reconnect automatically on next beacon. Also covers
      the IP/port/ApiKey settings that are currently read-only in
      the Settings tab.
- [ ] **Auto-update from GitHub Releases.**
      On startup (and periodically while running), the tray host queries
      the GitHub Releases API for the latest `server.v.*` tag. If a
      newer version exists, it downloads the new `Server.Tray.zip`
      release asset to a temp directory, verifies a SHA-256 hash
      published alongside the release, and prompts the user via a tray
      balloon ("Update available — click to install"). On confirmation,
      the tray host launches a small updater helper (or a self-script)
      that waits for the current process to exit, swaps the executable,
      and restarts. The Cli host does **not** auto-update — service-mode
      deployments should be updated through whatever deployment pipeline
      installed them (sc.exe, Ansible, etc.). Security: the GitHub API
      call must be HTTPS (it is by default), and the downloaded archive
      must be hash-verified before any swap. The update check is
      opt-out via `appsettings.json` (`Updates: { Enabled: true,
      CheckInterval: "24:00:00" }`). Requires a release-asset naming
      convention to be added to `publish-server.yml` (e.g.
      `PcBeaconAgent.Server.Tray-win-x64-{version}.zip`).
- [ ] Cross-device clipboard & file transfer.A lightweight "AirDrop-like" feature: 
      send arbitrary text,files, or clipboard contents from the Android client to themanaged PC, 
      and vice versa. Today the user works around thisby sending links/files to "Saved Messages" 
      in Telegram — aworkable but clumsy cross-device hop. A nativePOST /api/transfer endpoint 
      (text payloads first, binaryfiles later) with a SignalR push event to notify the receivingside 
      would make the PcBeaconAgent a single-purpose tool forthe "I just need to get this string to my PC" 
      problem. The trayhost is the natural receiver surface for incoming transfers.
- [ ] **Local AI agent integration with sandboxed tools.**
      Send and receive commands to a local AI agent (e.g. LM Studio,
      Ollama) running on the managed PC. The Android client types a
      prompt, the server forwards it to the local LLM via its HTTP API
      (OpenAI-compatible `POST /v1/chat/completions`), and streams the
      response back. The agent has access to a set of sandboxed tools
      invoked via the OpenAI function-calling protocol:

      - **File system** — read/write/list files inside one or more
        user-configured sandbox folders (not a single hardcoded path;
        the user adds/removes folders in Settings). All file paths
        are validated against the configured roots — no path traversal
        outside the sandbox. No process-level isolation (option b from
        the design discussion) — designated-folder + path validation
        is the security boundary.

      - **GitHub** — read access to issues, pull requests, commits,
        and releases via the GitHub REST API. Requires a
        user-provided personal access token (stored in appsettings.json
        or a separate config file). Public repos work without a token
        (rate-limited); the token lifts the rate limit and unlocks
        private repos.

      - **Web search** — DuckDuckGo (HTML scraping or the lite
        endpoint). No API key needed. Results are parsed and returned
        to the agent as structured text.

      The agent runs a multi-turn loop: user prompt → LLM responds
      with tool calls → server executes the tools (validating
      filesystem paths, checking the GitHub token, rate-limiting
      web search) → results fed back to the LLM → LLM produces the
      final answer → streamed to the client. The server exposes
      `POST /api/ai/chat` and a SignalR streaming hub method; the
      client shows a chat UI with tool-call visibility (so the user
      sees what the agent is doing). No model files are bundled —
      the user installs LM Studio / Ollama separately.

## Tier 4 — security hardening (deferred until TLS lands)

These items only make sense once the transport is encrypted, otherwise
they protect against the wrong threat model.

- [ ] **TLS / HTTPS for the Web API and SignalR.**
      Self-signed certificate generated on first run, pinned on the
      client during pairing. Until this lands, the PIN and the API key
      travel in cleartext over the LAN. Applies to both `Server.Cli`
      and `Server.Tray` — they share the same Kestrel pipeline and
      the same `Server.Core` configuration, so a single cert + bind
      update covers both hosts.
- [ ] **Authenticate `/api/pair/regenerate`.**
      Currently unauthenticated so the client can request a fresh PIN
      before it has a key. Once TLS is in place, require a short-lived
      bearer token issued at service start (printed in the log / tray)
      or rate-limit aggressively.
- [ ] **Drop API-key-via-query-string fallback in `BeaconHub`.**
      `IsAuthorized` reads `X-Api-Key` from the header, then falls back
      to `?api_key=` in the query string. Query strings are logged by
      proxies and browsers; remove the fallback once all clients send
      the header (SignalR `AccessTokenFactory` can carry it).
- [ ] **Encrypt `server.key` with DPAPI.**
      `BeaconServerIdentity.LoadOrCreateKey` writes the API key as
      plaintext to `server.key`. Use `ProtectedData.Protect` scoped to
      the current user so a file copy does not leak the key.
- [ ] **Remote shell (lowest priority).**
      ConPTY-based CMD/PowerShell over SignalR. Only after TLS, only
      with an explicit opt-in flag in `appsettings.json`, and only for
      scenarios where the host process runs elevated. The Windows
      console experience is poor enough that process management above
      is the recommended path for the common case.