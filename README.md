# PcBeaconAgentService <img src=".github/assets/beacon.png" align="right" height="45" alt="PcBeaconAgent Logo">

[![Release Status](https://github.com/vertigra/PcBeaconAgent/actions/workflows/release.yml/badge.svg)](https://github.com/vertigra/PcBeaconAgent/actions/workflows/release.yml)

### Description
A solution designed to monitor PC status and manage it remotely. It consists of a background agent service for Windows and cross-platform clients.

The **PcBeaconAgent.Service** is a Windows Background Service and Web API agent built on **.NET 10**. It monitors PC status, can send periodic beacon signals to a management server, and exposes a local Web API for client applications.

### Build Features
* **Single-File Executable**: The service compiles into a single, self-contained `.exe` file.
* **Trimmed**: All unused code is automatically removed during compilation to optimize production binary size.
* **Self-Contained**: The .NET 10 runtime is packed inside the executable, meaning no external .NET SDK installation is required on the target machine.

### CLI Arguments (Silent Mode)
By default, the agent duplicates all logs directly to the console window. For background or scripted execution, you can completely suppress terminal output using the following flags:

    Run in silent mode (logs will be written to the file only):
    ./PcBeaconAgent.Service.exe --no-console

    or:
    ./PcBeaconAgent.Service.exe --silent

### Automation & Deployment
Every time a tag matching the `v.*` pattern (e.g., `v.1.0.0`) is pushed, a GitHub Action is triggered to:
1. Compile and optimize the project specifically for `win-x64`.
2. Clean the build artifacts from debug symbols (`.pdb`) and temporary logs.
3. Publish a clean `zip` archive named like `PcBeaconAgent.Service-v.1.0.0-win-x64.zip` containing the release-ready `PcBeaconAgent.Service.exe` and the default `appsettings.json` directly to GitHub Releases.
