# AGENTS.md

## Project

Windows-only .NET 9 WinForms tray monitor. It has no main window: `Program.Main` starts `TrayApplicationContext`, which owns the `NotifyIcon`, menu, animation timer, and background `MetricsSampler`.

- `src/EasyMonitoring.Core` — pure logic (`net9.0`); no WinForms, LHM, or P/Invoke.
- `src/EasyMonitoring.App` — Windows/LHM adapters and tray UI (`net9.0-windows`, x64).
- `tests/EasyMonitoring.Core.Tests` — xUnit tests for Core only. UI has no automated xUnit tests.
- Published exe is `artifacts/publish/EasyMonitoring.exe` because the App project overrides `AssemblyName`; it is git-ignored.

## Commands

```powershell
dotnet build EasyMonitoring.sln -c Release
dotnet test EasyMonitoring.sln -c Release
dotnet test tests/EasyMonitoring.Core.Tests/EasyMonitoring.Core.Tests.csproj -c Release --filter "FullyQualifiedName~CpuLoadCalculatorTests"
```

Publish (stop any running EasyMonitoring first):

```powershell
dotnet publish src/EasyMonitoring.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish
```

CI runs on GitHub Actions: `.github/workflows/build.yml` restores, builds, and tests on pushes to `main` and pull requests. `.github/workflows/release.yml` builds the release exe on `v*` tags. There is no linter or formatter config; build + tests + diagnostic modes are the verification path.

## Runtime prerequisites

- .NET SDK 9 is required to build (9.0.319 verified).
- CPU temperature requires the **PawnIO 2.2.0** driver. The LHM NuGet package embeds PawnIO modules but does **not** install the driver:
  `winget install --id namazso.PawnIO -e`
- Without PawnIO, CPU temp is `N/A` (LHM reports 0); CPU load, GPU, and RAM still work.
- The app and QA modes run elevated via `app.manifest` (`requireAdministrator`). Build/test do not require admin.
- Do not publish while the app is running: the locked exe causes `MSB3026`/`MSB3027`/`MSB3021`.

## Non-obvious invariants

- CPU load uses `GetSystemTimes` P/Invoke. Do not replace it with `PerformanceCounter`: localized Windows counter names are unreliable on this machine.
- P/Invoke helpers live in `App/Native`. `GlobalMemoryStatusExNative` needs `EntryPoint = "GlobalMemoryStatusEx"`; without it the native call fails.
- LibreHardwareMonitorLib is pinned to **0.9.6** and is verified with PawnIO. Re-run `--diagnostics` before changing that version.
- Keep LHM work in `MetricsSampler`/`LhmMetricsSource` on the background task; UI updates go through `SynchronizationContext`. CPU load/animation sample every 1 s; LHM (CPU/GPU temp, GPU load) and RAM every 2 s; CPU/GPU load keep a 3-sample average. LHM hardware and selected sensors are resolved once at open — do not re-scan or re-select sensors per tick.
- Idle budget (verified 2026-10-20 on this machine, published exe): ~1.6% of one core and ~85 MB working set (under 2% / 100 MB). RunCat 365 3.6.0 measured ~8.2% of one core; change sampling intervals only with fresh measurements.
- Sensor priority is in `Core/Sensors/SensorSelector`: CPU `Core (Tctl/Tdie)` → `CPU Package` → `Core*`/`CCD*`; GPU NVIDIA → AMD → Intel, preferring `GPU Core` load/temp. Tests encode this behavior.
- A temperature of 0 or outside 0–150 °C is treated as missing, not as a real reading.
- DPI is configured by `ApplicationHighDpiMode=PerMonitorV2` in the App csproj. Do not add `dpiAware`/`dpiAwareness` back to `app.manifest`; the SDK emits warning `WFO0003`.
- In `Program.Main`, call `Application.SetUnhandledExceptionMode` before creating controls or a `WindowsFormsSynchronizationContext`, otherwise it throws. The fallback context in `TrayApplicationContext` is intentional: `SynchronizationContext.Current` is null before `Application.Run`.
- The tray app has no `Form`. `Exit` disposes the sampler, icons, menu, and `NotifyIcon`, then calls `ExitThread`; keep that lifecycle intact.
- UI text is English with invariant-culture numbers and `°C`; documentation is Russian. Preserve formats such as `CPU: 23% | 58 °C` and `RAM: 8.2 / 31.9 GB (26%)`.
- Menu theme is custom: `App/UI/MenuTheme` + `EasyMonitoringMenuRenderer`. Disabled menu text must stay white; the renderer overrides `OnRenderItemText` and draws the check mark. Do not replace it with default system colors.
- Autostart is a Task Scheduler task named `EasyMonitoring`, created from XML via `schtasks /Create`, with `InteractiveToken` and `HighestAvailable` so it starts elevated without a logon UAC prompt. The menu checkbox is `Start with Windows`; queries are read-only, toggles run in the background.
- Single-instance mutex is `Local\EasyMonitoring.SingleInstance`. `--diagnostics`, `--ui-check`, and `--autostart-check` intentionally bypass it and run headless.

## Verification helpers

After building or publishing:

```powershell
.\artifacts\publish\EasyMonitoring.exe --diagnostics     # metrics; writes diagnostics.txt and exits
.\artifacts\publish\EasyMonitoring.exe --ui-check        # tray/menu/theme/animation; writes ui-diagnostics.txt and exits
.\artifacts\publish\EasyMonitoring.exe --autostart-check # temporarily creates/removes the real task; writes autostart-diagnostics.txt
```

`--ui-check` also waits for the cached autostart state. Outputs and error log live in `%LOCALAPPDATA%\EasyMonitoring\`. Use `docs/qa-checklist.md` for current results and the remaining manual checks.

## Repo state

- GitHub: `MiniBeary/EasyMonitoring` (public), default branch `main`; local commits use `MiniBear <184402887+MiniBeary@users.noreply.github.com>`.
- `bin/`, `obj/`, `artifacts/`, `*.user`, and logs are ignored. Do not commit the published exe.
- `README.md` is the user-facing guide; `docs/easymonitoring-plan.md` is the implementation plan/status; `docs/qa-checklist.md` is the current QA record; `docs/runcat365-3.6.0-analysis.md` is reference analysis, not a build input.
- Release process: push a `v*` tag; `.github/workflows/release.yml` publishes the single-file exe, writes `SHA256SUMS.txt`, and creates a pre-release. Do not upload the exe manually unless the workflow fails.
- `LICENSE` is MIT; third-party notices and full license texts are in `THIRD-PARTY-NOTICES.md` and `docs/licenses/`.
