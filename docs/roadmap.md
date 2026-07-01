# Roadmap

This file tracks planned features, architectural changes, and known
follow-up items that are out of scope of the current iteration. It is
a living document — items move up when they become the next focus and
get a commit reference when they ship.

Items are grouped by priority tier, not by category. Within a tier the
order is loose.

## Tier 1 — polish the existing surface

These items are blocking "the current functionality is solid" and should
land before the tray host or any new feature module.

- [ ] **Display: disable the primary display correctly.**
      Currently `ApplyPathInfos` rejects the remaining path set because
      no display is promoted to primary. Need to set the primary flag on
      the surviving display before applying. Tracked in the display-fix
      thread.
- [ ] **Refactor `PairingViewModel` → `PairingServiceClient`.**
      `PairingViewModel` creates `HttpClient` via `IHttpClientFactory`
      inline in three places, which breaks the layering established by
      `AudioServiceClient` / `DisplayServiceClient`. Extract a
      stateless `PairingServiceClient` (+ `IPairingServiceClient`) in
      `Client.Core/Services` that takes `ip`/`port` per call (pairing
      happens before the device is in `ManagedDevices`, so the client
      cannot be bound to a single device like the other service clients).
- [ ] **`RandomNumberGenerator` for the PIN.**
      `PairingService.GeneratePin` uses `Random.Shared.Next`, which is
      not a CSPRNG. Switch to `RandomNumberGenerator.GetInt32(100_000,
      1_000_000)` so a captured broadcast cannot be used to predict the
      next PIN.
- [ ] **`lock` in `PairingService.ValidateAndExchangePin`.**
      The singleton's mutable fields (`mPin`, `mPinUsed`,
      `mFailedAttempts`) are read and written without synchronization.
      Two concurrent `/api/pair` calls can both succeed (PIN not marked
      used) or lose the failed-attempt counter. Wrap the validate+exchange
      critical section in a `lock`.

## Tier 2 — tray host

The `PcBeaconAgent.Server.Cli` console host stays. A new
`PcBeaconAgent.Server.Tray` project is added alongside it:

- WPF + `NotifyIcon` (WinForms interop for the tray icon).
- References `Server.Core` — reuses pairing, beacon, controllers, hub.
- Auto-start on user login via `HKCU\Software\Microsoft\Windows\
  CurrentVersion\Run` (no admin rights needed).
- Single-instance mutex.
- PIN shown in a window and in the tray tooltip, not only in the log
  file. New PIN appears immediately after a successful pairing or after
  the user clicks "Regenerate PIN" in the tray menu.
- Settings window: API key, ports, log path, auto-start toggle.
- The existing `Server.Cli` keeps working for headless / scripted /
  debug scenarios. Both hosts share the same `Server.Core` business
  logic, so no behaviour drift.

## Tier 3 — new feature modules

Roughly ordered by expected value. Each item will get its own design
note before implementation; the entries here are reminders.

- [ ] **Virtual gamepad (ViGEmBus).**
      Android UI exposes D-pad + buttons; the server creates a virtual
      Xbox 360 controller via `Nefarius.ViGEm.Client`. Requires the
      ViGEmBus driver to be installed on the host (bundled in the
      installer). Target use case: navigating gamepad-friendly apps
      (Steam Big Picture, media centers, emulators).
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