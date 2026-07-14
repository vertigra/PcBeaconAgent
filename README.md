# PcBeaconAgentService <img src=".github/assets/beacon.png" align="right" height="45" alt="PcBeaconAgent Logo">

[![Server Release](https://img.shields.io/github/v/tag/vertigra/PcBeaconAgent?filter=release.v.*&label=Server&color=blue)](https://github.com/vertigra/PcBeaconAgent/releases)
[![Client Release](https://img.shields.io/github/v/tag/vertigra/PcBeaconAgent?filter=release.v.*&label=Client&color=blue)](https://github.com/vertigra/PcBeaconAgent/releases)
[![Release CI](https://img.shields.io/github/actions/workflow/status/vertigra/PcBeaconAgent/publish-all.yml?label=Release%20CI)](https://github.com/vertigra/PcBeaconAgent/actions/workflows/publish-all.yml)
[![Tests](https://img.shields.io/github/actions/workflow/status/vertigra/PcBeaconAgent/tests.yml?branch=devel&label=Tests)](https://github.com/vertigra/PcBeaconAgent/actions/workflows/tests.yml)
[![Coverage](https://coveralls.io/repos/github/vertigra/PcBeaconAgent/badge.svg?branch=devel)](https://coveralls.io/github/vertigra/PcBeaconAgent?branch=devel)
[![CodeQL](https://github.com/vertigra/PcBeaconAgent/actions/workflows/github-code-scanning/codeql/badge.svg?branch=master)](https://github.com/vertigra/PcBeaconAgent/actions/workflows/github-code-scanning/codeql)

### 📋 Description
PcBeaconAgent is a remote control solution for Windows PCs. A lightweight
agent runs on the PC and exposes a local Web API + SignalR hub over the
LAN. An Android client discovers the agent via UDP broadcast, pairs via
a one-time PIN, and then can:

- **Switch audio output devices** — list playback devices, change the
  default output, without touching the Windows sound settings.
- **Control displays** — list connected monitors, disable a specific
  display, restore the original topology. The server correctly handles
  primary-display promotion and clone/extend modes.
- **Monitor online status** — the client sees which PCs are reachable
  in real time, and control buttons disable automatically when a device
  goes offline.

The server is a self-contained .NET 10 single-file executable. The
Android client is a .NET MAUI app. All traffic is plain HTTP (LAN-only);
TLS is planned for a future release.

The **PcBeaconAgent.Server.Cli** is a console host that runs the Web API
and SignalR hub. The **PcBeaconAgent.Server.Tray** is a WPF tray host
that runs the same business logic and adds a system tray icon, a PIN
popup with countdown, and balloon notifications. Both share the same
`Server.Core` — pick the one that fits your deployment:

- **Server.Cli** — interactive console host for debugging and scripted
  use. Detailed documentation: [docs/server-cli.md](docs/server-cli.md).
- **Server.Tray** — interactive desktop sessions; auto-starts on user
  login (planned), shows the PIN in a popup next to the taskbar.
  Detailed documentation: [docs/server-tray.md](docs/server-tray.md).

### 💻 CLI Arguments (Silent Mode)

For background or scripted execution, the CLI host can suppress
terminal output:

    ./PcBeaconAgent.Server.Cli.exe --no-console
    # or:
    ./PcBeaconAgent.Server.Cli.exe --silent

See [docs/server-cli.md](docs/server-cli.md) for the full CLI reference
(appsettings.json schema, log file location, interactive-only notes).

### 🧠 System Architecture

The solution is a monorepo with five projects arranged in a strict
layering. The dependency arrows never point from server to client —
both shared libraries depend on `Contracts`, never on each other.

```
Contracts (DTOs, ProjectJsonContext — no dependencies)
    ↑              ↑
    │              │
Client.Core    Server.Core
    ↑              ↑
    │              │
Android        Server.Cli  •  Server.Tray
```

* **`PcBeaconAgent.Contracts`** — wire contracts only (DTOs,
  `BeaconDevice`, `ProjectJsonContext`). No dependencies beyond the BCL.
  Referenced by both `Client.Core` and `Server.Core`.
* **`PcBeaconAgent.Client.Core`** — client-side business logic and UI
  support types (`ManagedDevice`, `SignalService`, `AudioServiceClient`,
  `DisplayServiceClient`, `PairingServiceClient`, `BeaconClient`).
  References `Contracts`.
* **`PcBeaconAgent.Server.Core`** — server-side business logic
  (`BeaconServiceHub`, `BeaconServer`, `BeaconServerIdentity`,
  `PairingService`, `DisplayController`, `AudioController`). Holds the
  `FrameworkReference Microsoft.AspNetCore.App` and the Windows-only
  NuGet packages (`WindowsDisplayAPI`, `AudioSwitcher`). References
  `Contracts`.
* **`PcBeaconAgent.Server.Cli`** — the console host (composition root,
  HTTP endpoint mapping, `BeaconBackgroundService`). Interactive-only —
  the controllers require a desktop session, so it cannot run as a
  Windows Service. References `Server.Core`. See
  [docs/server-cli.md](docs/server-cli.md).
* **`PcBeaconAgent.Server.Tray`** — the WPF tray host. Runs the same
  `Server.Core` business logic and adds a system tray icon, a PIN popup
  with countdown, and balloon notifications via `INotificationService`.
  Suitable for interactive desktop sessions. References `Server.Core`.
  See [docs/server-tray.md](docs/server-tray.md).
* **`PcBeaconAgent.Client.Android`** — the .NET MAUI Android client.
  References `Client.Core`.

**Data Flow & Security:**
The architecture follows a clear "Discovery-to-RPC" lifecycle to
maintain security while ensuring network flexibility.

1. **UDP Beacon (Discovery)**: The scanner identifies network devices
   via UDP broadcast. At this stage, only raw `IP:Port` metadata is
   available. This phase is unauthenticated.
2. **SignalR RPC (Request-Response)**: Once the user initiates a
   connection ("Remember"), the client establishes a persistent SignalR
   connection. Instead of event-based push patterns, the system uses
   **authenticated RPC calls** (`InvokeAsync`) to fetch device metadata.
   This ensures deterministic state management and tight control over
   data exposure.
3. **Storage Sync & Indexing**: Device identities are persisted via
   `IDeviceStorageService`. The client maintains a thread-safe index of
   stored API keys, allowing for granular management and preventing data
   corruption during concurrent pairing operations.
4. **Identity Tracking**: The `BeaconDevice` model utilizes robust
   `Equals`/`GetHashCode` overrides to ensure consistent identity
   tracking even if the network configuration changes.
 
### 🔐 Security & Pairing
The system utilizes a secure, PIN-based pairing mechanism to ensure that only authorized clients can access device metadata or control signals. The pairing process is designed with the "Principle of Least Privilege":

* **No Secret Exposure**: Discovery over UDP carries only `IP:Port`. API keys are never broadcasted.
* **Isolated Trust**: Each client stores an IP-scoped key. Compromising one device does not grant access to others.
* **Non-Persistent Discovery**: Listing devices does not require credentials; authentication is only triggered when a user explicitly initiates a connection ("Remember").
* **Key Lifecycle Management**: The client maintains a local index of stored API keys, allowing for granular "Forget" operations. This ensures that when a device is removed, its associated credentials are fully purged from the local Android Keystore, preventing "orphaned" trust entries.

For a technical deep-dive into the state machine, failure modes, and cryptographic storage isolation, refer to the **[PIN Pairing Algorithm Reference](docs/pairing-reference.md)**.

### ⚙️ Build Features
* **Single-File Executable**: The service compiles into a single, self-contained `.exe` file.
* **Trimmed**: All unused code is automatically removed during compilation to optimize production binary size.
* **Self-Contained**: The .NET 10 runtime is packed inside the executable, meaning no external .NET SDK installation is required on the target machine.

## 🚀 CI/CD Automation & Releases

The project utilizes automated deployment pipelines configured via **GitHub Actions**. A single `release.v.*` tag publishes both the server (Cli + Tray) and the Android client in one unified release.

### 🏷️ Release Tag Format

To trigger a release, push a tag matching the naming convention below. The version number `X.Y.Z` must follow the [versioning rules](CONTRIBUTING.md#versioning) defined in `CONTRIBUTING.md`.

| Component | Tag Pattern | Target Workflow | Release Name Example |
| :--- | :--- | :--- | :--- |
| **All (Server + Client)** | `release.v.X.Y.Z` | `publish-all.yml` | `Release X.Y.Z` |

> 💡 **Branch & Tag Isolation Note:** Git tags point directly to a specific commit, completely independent of branches. You can safely create and push release tags from development branches (e.g., `devel`). GitHub Actions will check out and compile the exact commit historical snapshot bound to that tag, provided that the corresponding workflow `.yml` file exists within that commit.

> ⚠️ **Important:** The version string `X.Y.Z` must follow strict semantic versioning numbers (e.g., `2.0.0`). Do not add extra prefixes or suffixes, otherwise the tag parsing engine in the pipeline will fail.

---

### 🛠️ How to Publish a New Version

1. Commit and push all your changes to the remote branch (e.g., `devel`).
2. Create and push the release tag:

        git tag release.v.2.0.0
        git push origin release.v.2.0.0

3. Navigate to the **Actions** tab of your GitHub repository to monitor the live build logs.

---

### ⚙️ Internal Version Processing Mechanics

#### 🖥️ Server (PcBeaconAgent.Server.Cli + PcBeaconAgent.Server.Tray)
* **Pipeline Output:** Both hosts compile to standalone, native Windows
  x64 single-file executables, each packed inside its own `.zip`
  archive.
* **Compilation Flags:** `Server.Cli` uses trimming
  (`-p:PublishTrimmed=true`); `Server.Tray` is published without
  trimming — WPF XAML bindings trim unreliably.
* **Metadata Extraction:** The pipeline strips the `release.v.`
  prefix and injects the raw `X.Y.Z` value into both executables'
  `Version` and `AssemblyVersion` properties.

#### 📱 Android Client (PcBeaconAgent.Client.Android)
* **Pipeline Output:** A standalone `.apk` package.
* **Version Code Calculation:** Android requires a monotonically
  increasing integer for its `versionCode`. The pipeline computes
  this from the tag:

    `VersionCode = (Major × 1,000,000) + (Minor × 1,000) + Build`

    *Example:* Tag `release.v.2.0.0` results in:
    - `versionCode` = `2,000,000`
    - `versionName` = `2.0.0`

---
## 🤝 Development Standards

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification to maintain a clean project history and enable automated changelog generation.

* Before contributing, please review the [CONTRIBUTING.md](CONTRIBUTING.md) file for details on commit message formats, types, and scopes.
* For coding conventions (naming, logging, DI, async, JSON), see the **[Coding Guidelines](docs/coding-guidelines.md)**.
* For the project roadmap and planned features, see the **[Roadmap](docs/roadmap.md)**.
* For test structure and what is covered, see **[Testing](docs/testing.md)**.
