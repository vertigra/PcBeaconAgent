# PcBeaconAgentService <img src=".github/assets/beacon.png" align="right" height="45" alt="PcBeaconAgent Logo">

[![Publish Server Release](https://github.com/vertigra/PcBeaconAgent/actions/workflows/publish-server.yml/badge.svg)](https://github.com/vertigra/PcBeaconAgent/actions/workflows/publish-server.yml)
[![Publish Android Client Release](https://github.com/vertigra/PcBeaconAgent/actions/workflows/publish-client.yml/badge.svg)](https://github.com/vertigra/PcBeaconAgent/actions/workflows/publish-client.yml)

### 📋 Description
A solution designed to monitor PC status and manage it remotely. It consists of a background agent service for Windows and cross-platform clients.

The **PcBeaconAgent.Service** is a Windows Background Service and Web API agent built on **.NET 10**. It monitors PC status, can send periodic beacon signals to a management server, and exposes a local Web API for client applications.

### ⚙️ Build Features
* **Single-File Executable**: The service compiles into a single, self-contained `.exe` file.
* **Trimmed**: All unused code is automatically removed during compilation to optimize production binary size.
* **Self-Contained**: The .NET 10 runtime is packed inside the executable, meaning no external .NET SDK installation is required on the target machine.

### 💻 CLI Arguments (Silent Mode)
By default, the agent duplicates all logs directly to the console window. For background or scripted execution, you can completely suppress terminal output using the following flags:

    Run in silent mode (logs will be written to the file only):
    ./PcBeaconAgent.Service.exe --no-console

    or:
    ./PcBeaconAgent.Service.exe --silent

## 🚀 CI/CD Automation & Releases

The project utilizes automated deployment pipelines configured via **GitHub Actions**. Server and client applications are completely decoupled and managed using independent versioning tracks via Git tags.

### 🏷️ Release Tag Formats

To trigger a release workflow, push a tag matching one of the strict naming conventions below from your local terminal:

| Component | Tag Pattern | Target Workflow | Release Name Example |
| :--- | :--- | :--- | :--- |
| **Windows Service (Server)** | `server.v.X.Y.Z` | `publish-server.yml` | `Server Release X.Y.Z` |
| **Android App (Client)** | `client.v.X.Y.Z` | `publish-client.yml` | `Client Android Release X.Y.Z` |

> 💡 **Branch & Tag Isolation Note:** Git tags point directly to a specific commit, completely independent of branches. You can safely create and push release tags from development branches (e.g., `devel`). GitHub Actions will check out and compile the exact commit historical snapshot bound to that tag, provided that the corresponding workflow `.yml` file exists within that commit.

> ⚠️ **Important:** The version string `X.Y.Z` must follow strict semantic versioning numbers (e.g., `1.0.4`). Do not add extra prefixes or suffixes, otherwise the tag parsing engine in the pipeline will fail.

---

### 🛠️ How to Publish a New Version

1. Commit and push all your changes to the remote branch (e.g., `main`).
2. Create and push the component-specific tag using your Git CLI:

        # Example: Deploying a new Server build
        git tag server.v.1.2.0
        git push origin server.v.1.2.0

        # Example: Deploying a new Android Client build
        git tag client.v.1.0.5
        git push origin client.v.1.0.5

> ⚠️ **Simultaneous Release Warning (Same Commit):** If you need to release both the Server and Client from the exact same commit snapshot, **do not** push their tags sequentially in separate commands. GitHub Actions may ignore the second trigger. Instead, create both tags locally and push them simultaneously using a single command:
> ```bash
> git push origin server.v.1.2.0 client.v.1.0.5
> # Or push all local tags at once:
> git push origin --tags
> ```

3. Navigate to the **Actions** tab of your GitHub repository to monitor the live build logs.

---

### ⚙️ Internal Version Processing Mechanics

#### 🖥️ Server (PcBeaconAgent.Service)
* **Pipeline Output:** A standalone, native Windows x64 single-file executable packed inside a `.zip` archive.
* **Compilation Flags:** Automated trimming (`-p:PublishTrimmed=true`), dead code elimination, and embedded assembly compilation attributes.
* **Metadata Extraction:** The pipeline strips the `server.v.` prefix and injects the raw `X.Y.Z` value directly into the executable's `Version` and `AssemblyVersion` properties. You can verify this inside the compiled binary properties in Windows Explorer.

#### 📱 Android Client (PcBeaconAgent.Client.Android)
* **Pipeline Output:** A standalone, signed optimization architecture `.apk` package.
* **Version Code Calculation:** Android requires a monotonically increasing integer for its `versionCode`. The pipeline automatically computes this value on the fly from your tag using a positioning multiplier formula:

      $$	ext{VersionCode} = (	ext{Major} 	imes 10000) + (	ext{Minor} 	imes 100) + 	ext{Build}$$

* *Example:* Pushing tag `client.v.2.4.12` instantly compiles an APK injected with `versionCode="20412"` and `versionName="2.4.12"`.

---
