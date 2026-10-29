# EasyMonitoring v1.0.2

Security and reliability update for Windows x64.

## Changes

- **Protected autostart:** elevated Task Scheduler registration now requires verified owners and permissions for the executable and its directory chain. Writable or unverifiable locations are refused. Manual portable launch remains available.
- **Safe migration:** a proven unsafe legacy task belonging to the current Windows identity is removed with a notification. Foreign tasks and tasks with uncertain ownership or protection are left untouched.
- **Reliable task state:** missing tasks are distinguished from access failures, timeouts and malformed XML. Unknown state remains retryable. Unicode paths survive Task Scheduler XML queries on legacy Windows code pages.
- **Sampler shutdown:** slow hardware reads finish before the source is released; late readings are not published. Shutdown keeps its bounded two-second wait.
- **Diagnostics:** autostart checks use unique temporary tasks and mandatory cleanup. UI diagnostics inspect the normal task without changing it.
- **Documentation:** the English README now covers safe autostart, updates, troubleshooting and build instructions.

The interface, Windows x64/.NET 9 target and 1 s/2 s sampling cadence are unchanged. Validation includes 121 automated tests and isolated Windows Task Scheduler tests.

## Updating from v1.0.1

1. Exit any running EasyMonitoring instance.
2. Download `EasyMonitoring-win-x64.exe` and verify it against `SHA256SUMS.txt`.
3. For **Start with Windows**, use elevated PowerShell to copy the replacement into an administrator-protected local directory, then run that copy and enable autostart. See the [README instructions](https://github.com/MiniBeary/EasyMonitoring/blob/v1.0.2/README.md#safe-windows-autostart).
4. A manually started portable copy still works. Autostart from a user-writable directory is refused; the app does not install itself or repair permissions.

A previous unsafe autostart task may be removed when this version starts. Re-enable autostart from the protected copy. If the directory stays the same, exit the app before replacing the EXE and check autostart after restarting.

## Requirements and verification

Windows 10/11 x64 and administrator rights are required. The release EXE is self-contained; a separate .NET installation is unnecessary. CPU temperature requires the separately installed PawnIO driver; without it CPU temperature displays `N/A`.

```powershell
(Get-FileHash .\EasyMonitoring-win-x64.exe -Algorithm SHA256).Hash.ToLower()
Get-Content .\SHA256SUMS.txt
```

The EXE is unsigned, so SmartScreen may warn before launch. GPU sensor availability depends on hardware and drivers. Actual sign-in and visual DPI checks remain manual; no fresh performance comparison is claimed.

EasyMonitoring uses the MIT license. See [third-party notices](https://github.com/MiniBeary/EasyMonitoring/blob/v1.0.2/THIRD-PARTY-NOTICES.md) for dependency licenses.
