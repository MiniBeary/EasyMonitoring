# EasyMonitoring

**CPU, GPU and RAM at a glance — in the Windows system tray.**

An animated arc follows CPU load. Hover for CPU usage and temperature; right-click for GPU, memory and autostart. No main window or account is required.

[![Build](https://github.com/MiniBeary/EasyMonitoring/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/MiniBeary/EasyMonitoring/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/MiniBeary/EasyMonitoring?include_prereleases&label=release&style=flat-square)](https://github.com/MiniBeary/EasyMonitoring/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)
[![Windows x64](https://img.shields.io/badge/Windows-10%2F11%20x64-0078D4?style=flat-square&logo=windows)](https://github.com/MiniBeary/EasyMonitoring/releases)

<p align="center">
  <img src="docs/assets/menu.png" alt="EasyMonitoring tray menu showing CPU, GPU, RAM and Windows autostart" width="260">
</p>

> **Branch status:** the autostart protection and shutdown fixes described below are in the current source and are not included in release **v1.0.1**. No new release has been published for these changes.

**[Download releases](https://github.com/MiniBeary/EasyMonitoring/releases)** · [Build from source](#build-from-source) · [Report a security issue](SECURITY.md)

## Quick start

1. Download `EasyMonitoring-win-x64.exe` from Releases, or build the current source.
2. For **CPU temperature**, install the separate PawnIO driver:
   ```powershell
   winget install --id namazso.PawnIO -e
   ```
3. Run the exe and approve the Windows administrator prompt.
4. Find the arc in the notification area or its overflow menu. Right-click → **Exit** closes the app.

Without PawnIO, CPU temperature displays `N/A`; CPU usage, GPU and RAM remain available.

| In the tray | What you see |
| --- | --- |
| Animated icon | Five arc frames; animation speeds up with CPU load |
| Hover | `CPU: 23% \| 58 °C` |
| Right-click menu | CPU/GPU usage and temperature, RAM used/total, autostart and Exit |

## Requirements

- **Windows 10/11 x64** and administrator rights.
- **PawnIO** for CPU temperature. Available GPU sensors depend on hardware and drivers.
- Published builds are self-contained: a separate .NET installation is unnecessary.
- Building from source requires the **.NET 9 SDK**.

The exe is unsigned. Windows SmartScreen can warn before launch; administrator elevation is still required for a manual start.

## Safe Windows autostart

Choose **Start with Windows** in the tray menu. The current source permits elevated autostart only when the executable and its directory chain are protected from modification by unprivileged users.

A directory named `Program Files` is not sufficient on its own: ownership and actual permissions are checked. Unknown permissions, network paths, SUBST drives and reparse points are refused.

### Put a copy in a protected folder

First exit any running copy. Open **PowerShell as administrator**, navigate to the downloaded exe and run:

```powershell
$destination = Join-Path $env:ProgramFiles 'EasyMonitoring'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath '.\EasyMonitoring-win-x64.exe' -Destination (Join-Path $destination 'EasyMonitoring.exe')
& (Join-Path $destination 'EasyMonitoring.exe')
```

Then enable **Start with Windows**. Use a **copy**, rather than moving an existing folder: moves can retain its old permissions. The app verifies the result and does not change permissions or install itself.

- If protection cannot be verified, registration is refused and the reason is shown. Manual portable launch still works.
- A verified unsafe legacy `EasyMonitoring` task for the current Windows identity is removed, with a notification. Tasks belonging to another identity, or with uncertain ownership/protection, are left untouched.
- The task uses `InteractiveToken`, `HighestAvailable` and a logon trigger. Once safely enabled, it starts at sign-in without another UAC prompt.
- Clearing the checkbox removes the task. Query failures remain unknown and retryable.
- To update: exit the app, copy the replacement exe into the same protected location using elevated PowerShell, restart, and check autostart. If the location changes, enable autostart again from the new copy.

### Verify a release download

Compare the exe's SHA-256 with the release's `SHA256SUMS.txt`:

```powershell
(Get-FileHash .\EasyMonitoring-win-x64.exe -Algorithm SHA256).Hash.ToLower()
Get-Content .\SHA256SUMS.txt
```

## Diagnostics and troubleshooting

The current source provides three modes that exit automatically:

```powershell
.\EasyMonitoring.exe --diagnostics
.\EasyMonitoring.exe --ui-check
.\EasyMonitoring.exe --autostart-check
```

| Mode | Output in `%LOCALAPPDATA%\EasyMonitoring\` | Effect |
| --- | --- | --- |
| `--diagnostics` | `diagnostics.txt` | Read local metrics |
| `--ui-check` | `ui-diagnostics.txt` | Inspect hidden tray objects and autostart state; do not repair the user task |
| `--autostart-check` | `autostart-diagnostics.txt` | Create/check/remove a uniquely named diagnostic task; requires a protected exe location |

The diagnostic task is cleaned up in `finally`; the normal `EasyMonitoring` task is not used. Version v1.0.1 instead tests the real task and conditionally restores its enabled state.

Errors are logged to `EasyMonitoring.log` in the same directory. Review logs and reports for local paths and environment details before sharing them.

| Symptom | Check |
| --- | --- |
| CPU temperature is `N/A` | Install PawnIO, then restart EasyMonitoring |
| GPU readings are `N/A` | Check the GPU driver and available LibreHardwareMonitor sensors |
| Autostart is refused | Read the protection error and use a properly protected local copy |
| Icon is missing | Check the notification-area overflow menu and whether another instance is running |
| Tooltip seems unchanged | Move the pointer away and hover again; Windows may retain an open tooltip |

## Build from source

```powershell
dotnet build EasyMonitoring.sln -c Release
dotnet test EasyMonitoring.sln -c Release
```

Ordinary tests do not require elevation. Isolated filesystem and Task Scheduler integration tests run when the test process is elevated; otherwise they are explicitly skipped.

To publish, first stop any executable using the output folder:

```powershell
dotnet publish src/EasyMonitoring.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish
```

Output: `artifacts/publish/EasyMonitoring.exe`. Publishing locally does not create a GitHub release.

## How it works

| Reading | Source |
| --- | --- |
| CPU usage | Windows `GetSystemTimes` |
| CPU temperature | LibreHardwareMonitorLib 0.9.6 + PawnIO |
| GPU usage / temperature | LibreHardwareMonitor GPU sensors |
| RAM | Windows `GlobalMemoryStatusEx` |

CPU is sampled every second; hardware and RAM every two seconds. Missing values are retained for one cycle, then shown as `N/A`. Metrics run in the background; slow reads finish before their resources are released.

The application has no network or telemetry feature. Third-party hardware and runtime implementations are separate dependencies.

## Project notes

- [Architecture and domain vocabulary](CONTEXT.md)
- [QA results and dated performance measurements](docs/qa-checklist.md) — reference-machine results, not a guarantee for other systems
- [Implementation history](docs/easymonitoring-plan.md)
- [Security reporting](SECURITY.md)

There are no theme/settings panels. The dark menu is built in; the app shows one selected GPU.

## License

EasyMonitoring is licensed under **[MIT](LICENSE)**. LibreHardwareMonitorLib uses MPL-2.0; the runtime and other dependencies retain their own licenses. See [third-party notices](THIRD-PARTY-NOTICES.md).
