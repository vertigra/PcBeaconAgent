# PIN Pairing — Algorithm Reference

This document describes, in detail, how a client authenticates with a
`PcBeaconAgent.Server.Cli` instance for the first time, and how the resulting
key is stored, reused, and revoked afterward.

For a high-level overview, see the **🔐 Security & Pairing** section in
[README.md](README.md). This document goes one level deeper — it's the
reference to consult when modifying the pairing code itself.

---

## 1. Design goals

| Goal | How it's achieved |
|---|---|
| No secret travels over broadcast traffic | UDP discovery carries only `IP:port`, never a key |
| Scanning the network requires no credentials | Discovery and pairing are fully decoupled (see §3) |
| One compromised device key shouldn't affect others | Each server has its own independently generated key; each client stores one key *per server*, not one global key |
| Brute-forcing the PIN over LAN is impractical | 6-digit PIN, single-use, 5-minute TTL, lockout after 5 failed attempts |
| Forgetting a device should be final | `Forget` removes the stored key, not just the live connection |

---

## 2. State machine (server side)

`PairingService` is a singleton with the following state, scoped to the
process lifetime:

```
                 ┌─────────────────────┐
   service start │                     │ RegeneratePin()
   ──────────────▶      ACTIVE         │◀────────────────────┐
                 │  (PIN valid, unused)│                     │
                 └─────────┬───────────┘                     │
                           │                                 │
            correct PIN    │           wrong PIN             │
            submitted      │           (attempts < 5)        │
                           │                                 │
                  ┌────────▼──────────┐           ┌──────────┴────────┐
                  │      USED         │           │  attempts < 5?    │
                  │ (single-use,      │           │  yes → stay ACTIVE│
                  │  key returned)    │           │  no  → LOCKED     │
                  └───────────────────┘           └───────────────────┘
                           │                                  │
                  TTL expired                                 │
                  (5 minutes)                                 │
                           ▼                                  ▼
                  ┌───────────────────────────────────────────┐
                  │                  INACTIVE                 │
                  │  (PIN expired / used / locked — /api/pair │
                  │   returns 403 until RegeneratePin() is    │
                  │   called)                                 │
                  └───────────────────────────────────────────┘
```

`IsPairingActive` is `true` only while: not used, attempts `< 5`, and
`DateTime.UtcNow < expiry`. Any other state returns `403 Forbidden` from
`/api/pair`, distinct from `401 Unauthorized` (wrong PIN, but pairing is
still active).

---

## 3. Why discovery never asks for a PIN

Scanning the network (`StartScanAsync` → UDP broadcast → `OnBeaconFound`)
intentionally does **not** touch the SignalR hub at all. It only reads
`IP` and `Port` from the raw UDP `pong` reply and adds a bare `BeaconDevice`
to `DiscoveredDevices`.

This was a deliberate fix to an earlier version of the client, where
`OnBeaconFound` called the hub immediately for every discovered beacon —
which silently required a valid API key just to **list** devices, before
the user had decided to do anything with them.

The only client action that talks to the hub — and therefore the only
action that can throw `NotPairedException` — is the explicit **"Remember"**
button press.

```
┌─────────────┐        UDP only          ┌-──────────────────────┐
│ Start Scan  │  ───────────────────────▶│ DiscoveredDevices     │
│ (no auth)   │    IP + Port only        │ (IP, Port; no name/MAC│
└─────────────┘                          │  until paired)        │
                                         └──────────┬────────────┘
                                                    │
                                          user clicks "Remember"
                                                    │
                                                    ▼
                                       ┌─────────────────────────────┐
                                       │ ConnectAndFetchDetailsAsync │   ← first point
                                       │  (device)                   │     a key is
                                       │  requires X-Api-Key         │     required
                                       └──────────┬──────────────────┘
                                                  │
                              key missing/invalid │ key valid
                              ────────────────────┤────────────────────
                                                  ▼
                                  NotPairedException        GetDeviceDetails()
                                  → navigate to              returns data via
                                  PairingPage                direct RPC call →
                                                              RememberDevice
                                                              (connection stays open,
                                                               no reconnect)
```

---

## 4. Full pairing sequence

```
Server                                  Client
──────                                  ──────
Service starts
  │
  ├─ BeaconServerIdentity
  │    resolves ApiKey (appsettings.json
  │    static value, or generates +
  │    persists to server.key)
  │
  ├─ PairingService generates PIN
  │    (eagerly instantiated in Program.cs
  │    so the PIN appears in the log
  │    before the first request)
  │
  ▼
"PAIRING PIN: 847291, valid 5 min"
  (printed to console + log file)


                                         User taps "Start Scan"
                                           │
                            UDP ping  ◀────┤
                            UDP pong  ────▶│  IP + port only, no key
                                           │
                                         DiscoveredDevices.Add(ip, port)
                                           │
                                         User taps "Remember"
                                           │
                            POST /hubs/beacon/negotiate (no key yet)
                                       ◀───┤  — connection succeeds at
                                           │    transport level; auth
                                           │    happens in OnConnectedAsync
                            X-Api-Key: ""  │
BeaconHub.IsAuthorized() = false  ────────▶│
Context.Abort()                           │
                                       ◀───┤  NotPairedException
                                           │
                                         GoToAsync(PairingPage?ip=..&port=..)
                                           │
                                         User reads PIN from server console,
                                         types it in, taps "Pair"
                                           │
                            POST /api/pair { "pin": "847291" }
                                       ◀───┤
PairingService.ValidateAndExchangePin()   │
  PIN matches, not expired, not used      │
  → mark used, return ApiKey               │
                                           │
{ "apiKey": "a3f8c2..." }  ──────────────▶│
                                           │
                                         SecureStorage["api_key:<ip>"] = key
                                         GoToAsync("//MainPage")
                                           │
                                         User taps "Remember" again
                                           │
                            ConnectAndFetchDetailsAsync(device)
                            X-Api-Key: a3f8c2...
                                       ◀───┤  — single persistent connection,
                                           │    established once
BeaconHub.IsAuthorized() = true           │
                                           │
                            connection.InvokeAsync<BeaconDevice>
                            ("GetDeviceDetails")              direct RPC call:
                                       ◀────────────────────── client invokes a
BeaconHub.GetDeviceDetails()              │                    hub method and
  returns BeaconDevice         ──────────▶│                    awaits its return
                                           │                    value — no push,
                                           │                    no event, no
                                           │                    separate wait step
                                           │
                                         UpdateDeviceInfo(device, details)
                                         managed = DeviceStore.RememberDevice(device)
                                         managed.IsOnline = true   ◀── see note below
                                           │
                                         Connection stays open — used directly
                                         for ongoing status (DeviceStatusChanged)
                                         and future commands. No second connect,
                                         no close, no reconnect.
                                           │
                                         ManagedDevices now contains device
                                         DiscoveredDevices.Remove(device)
```

> **Why `managed.IsOnline = true` is set explicitly:** `ConnectAsync` raises
> `DeviceStatusChanged(ip, true)` right after `StartAsync()` succeeds — but at
> that point the device is still only in `DiscoveredDevices`, not yet in
> `ManagedDevices`. `MainViewModel.OnDeviceStatusChanged` looks the device up
> in `ManagedDevices` and silently no-ops if it isn't found yet, so that first
> "online" notification is lost. By the time `RememberDevice` adds the device,
> `DeviceFactory.Create` has already produced a `ManagedDevice` with the
> default `IsOnline = false`, and nothing corrects it afterward — without the
> explicit assignment, a freshly paired device would show as "Offline"
> immediately after Remember, despite having a live connection. Reaching this
> line without an exception is itself the proof the connection is live, so
> setting it directly is correct, not just a workaround.

> **Historical note:** this flow went through three designs. (1) Originally,
> a one-shot `ReceiveDeviceDetailsAndCloseAsync` connected, requested details,
> and was told by the server to close (`CloseConnection` message), followed
> by a second, separate persistent `ConnectToBeaconHubAsync` call — two
> connections for one logical action. (2) That was simplified so the server
> pushed details automatically in `OnConnectedAsync`, awaited client-side via
> a temporary `DeviceDetailsReceived` subscription + `TaskCompletionSource` +
> timeout — one connection, but an unnecessary push/wait dance for something
> that's really just a request/response. (3) The current design: the client
> already knows it wants details right after connecting, so it just asks for
> them — `BeaconHub.GetDeviceDetails()` is a plain hub method with a return
> value, called via `connection.InvokeAsync<BeaconDevice>(...)`. No push, no
> event, no `TaskCompletionSource`, no `CloseConnection` — standard SignalR
> request/response. This also enabled `RefreshDeviceDetailsAsync`, which
> reuses the same RPC call on an already-open connection (e.g. to refresh a
> `ManagedDevice`'s info on reconnect, without a fresh handshake).

---

## 5. Key storage scheme

| Action | Storage key | Notes |
|---|---|---|
| PIN pairing succeeds | `api_key:{ip}` | One key per server, scoped by IP address |
| Manual entry via SettingsPage | `api_key` | Global fallback, only used if no per-IP key exists for that server (see `SignalService.ResolveApiKey`) |
| Device forgotten | `api_key:{ip}` removed | `SignalService.ForgetAsync` disconnects **and** deletes the key — a plain `Disconnect` (e.g. app going to sleep) never deletes it |

Lookup order on every connection attempt:

```
ResolveApiKey(ip):
    1. api_key:{ip}   — found? → use it
    2. api_key        — found? → use it (static/manual key)
    3. neither found  → throw NotPairedException
```

### Enumerating stored keys (SettingsPage)

`SecureStorage` (Android Keystore) has no API to list its own keys — only
to read a value by a known name. To show "all keys ever stored" on the
Settings screen, `MauiPreferencesService` maintains a separate **index**:
a plain JSON array of key names, stored in `Preferences` (not
`SecureStorage` — the list of *identifiers* isn't secret, only the *values*
are). The index is updated alongside every `Set`/`Remove` on an `api_key*`
key, under a `lock` to avoid two concurrent pairings corrupting it via a
read-modify-write race.

`SettingsViewModel.LoadStoredKeys()` cross-references each identifier
against `IDeviceStorageService.LoadDevices()` to show `MachineName` /
`MacAddress` next to the IP, when the device is still remembered.

---

## 6. Timeouts and failure modes

| Scenario | Behavior |
|---|---|
| PIN expires before user submits it | `/api/pair` → `403`; client shows "Pairing mode is inactive" |
| Wrong PIN, attempts remain | `/api/pair` → `401`; client shows "Wrong PIN" |
| 5 wrong attempts in a row | Pairing locks until service restart, or until `/api/pair/regenerate` is called |
| Server responds but device details never arrive | `RefreshDeviceDetailsAsync` (called internally by `ConnectAndFetchDetailsAsync`) throws `TimeoutException` after 5s — `connection.InvokeAsync` is given a linked, time-limited `CancellationToken`; if it cancels without the caller's own token having been cancelled, that's treated as a timeout rather than a deliberate cancellation |
| Stored key no longer valid (server key rotated, app reinstalled on a different device, etc.) | Next connection attempt throws `NotPairedException`; for `ManagedDevices` this is caught in `App.ConnectToManagedDevicesAsync` and redirects to `PairingPage` automatically on next resume |

---

## 7. Things intentionally out of scope

- **Transport encryption.** The PIN exchange and all subsequent traffic are
  plain HTTP. The PIN flow protects against *unauthenticated* access, not
  against an on-path attacker capturing the exchange in real time. If that
  threat matters for your deployment, put a reverse proxy with TLS in front
  of the service.
- **Multi-user pairing.** Only one PIN exists at a time per server process.
  Two people pairing simultaneously will race for the same PIN; the second
  submission after the first succeeds will get `403` (already used) and
  needs `RegeneratePin()`.
