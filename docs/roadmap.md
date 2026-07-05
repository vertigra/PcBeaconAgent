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

- WPF + `NotifyIcon` (WinForms interop for the tray icon).
- References `Server.Core` — reuses pairing, beacon, controllers, hub.
- Auto-start on user login via `HKCU\Software\Microsoft\Windows\
  CurrentVersion\Run` (no admin rights needed).
- Single-instance mutex. The mutex must be shared between the tray host
  and the console host — if the tray is running, `Server.Cli` must
  refuse to start (and vice versa). Both hosts bind the same UDP
  discovery port and the same HTTP port, so a second instance would
  crash on socket bind anyway; the mutex gives a clean error message
  instead of an obscure `AddressAlreadyInUse`.
- PIN shown in a window and in the tray tooltip, not only in the log
  file. New PIN appears immediately after a successful pairing or after
  the user clicks "Regenerate PIN" in the tray menu.
- Settings window: API key, ports, log path, auto-start toggle.
- The existing `Server.Cli` keeps working for headless / scripted /
  debug scenarios. Both hosts share the same `Server.Core` business
  logic, so no behaviour drift.
- [ ] **CI/CD for Server.Tray.**
      The `publish-server.yml` workflow should build and publish
      `Server.Tray` alongside `Server.Cli` — both are server-side
      executables and should be released together under the same
      `server.v.X.Y.Z` tag. The release artifact should include both
      ZIPs (or a combined ZIP).
- [ ] **Documentation for Server.Tray.**
      Update `README.md` with a section describing the tray host: what
      it is, how to run it, how it differs from `Server.Cli`, and how
      to configure auto-start. Update the CI/CD section to mention
      that the server release now includes both hosts.
- [ ] **Add unit and integration tests.**
      The project currently has no test projects. Add a
      `PcBeaconAgent.Server.Core.Tests` project (xUnit) covering the
      pure-logic services: `PairingService` (PIN state machine, lockout,
      expiry, thread safety), `BeaconServerIdentity` (key loading /
      generation). Add a `PcBeaconAgent.Client.Core.Tests` project
      covering `DeviceStore`, `DeviceFactory`, `SignalService` (mocked
      SignalR). Controllers that wrap Windows-only COM / CCD APIs
      (`AudioController`, `DisplayController`) are tested manually on
      a real machine — mark them with `[Trait("Category", "RequiresWindows")]`
      so CI can skip them.

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
- [ ] Cross-device clipboard & file transfer.A lightweight "AirDrop-like" feature: 
      send arbitrary text,files, or clipboard contents from the Android client to themanaged PC, 
      and vice versa. Today the user works around thisby sending links/files to "Saved Messages" 
      in Telegram — aworkable but clumsy cross-device hop. A nativePOST /api/transfer endpoint 
      (text payloads first, binaryfiles later) with a SignalR push event to notify the receivingside 
      would make the PcBeaconAgent a single-purpose tool forthe "I just need to get this string to my PC" 
      problem. The trayhost is the natural receiver surface for incoming transfers.

## Tier 4 — security hardening (deferred until TLS lands)

These items only make sense once the transport is encrypted, otherwise
they protect against the wrong threat model.

- [ ] **TLS / HTTPS for the Web API and SignalR.**
      Self-signed certificate generated on first run, pinned on the
      client during pairing. Until this lands, the PIN and the API key
      travel in cleartext over the LAN.
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