# EasyMonitoring — Context

## Domain vocabulary

- **Metrics observation** — one tick’s raw CPU load plus the slow hardware/memory observations. The App adapter reads it; the Core module decides what to do with it.
- **Metrics snapshot** — the values presented to the tray. It is produced by one deep Core module that owns cadence, freshness, averaging, and missing-value policy.
- **Freshness** — whether a value came from the current sampling interval. A missing value is held for one cycle, then becomes `null`/`N/A`.
- **Autostart state** — actual versus desired Task Scheduler state, including known/unknown, last check time, last error, and task health. Unknown is not the same as disabled.
- **Sensor binding** — resolving a stable `SensorId`/`HardwareId` in Core and mapping it to a live LHM `ISensor`/`IHardware` in the App adapter. Binding happens once; reads do not search by name.
- **Tray host** — composition and lifecycle module. It owns the sampler, autostart state module, presentation adapter, and shutdown order.
- **Tray presentation** — display state and the thin WinForms adapter. `TrayPresentationState` owns text, animation, and autostart display; `TrayPresentation` applies it to `NotifyIcon` and the menu.

## Invariants

- Core stays pure: no WinForms, LibreHardwareMonitor, or P/Invoke.
- CPU load uses `GetSystemTimes`; LHM is pinned to 0.9.6 and sensors resolve once.
- CPU load is sampled every 1 s; LHM temperatures/GPU load and RAM every 2 s.
- Missing readings are held for one cycle, then become `N/A`; GPU averages only fresh slow samples.
- CPU temperature falls back only to recognized CPU sensors; an unrelated temperature is not a CPU temperature.
- Autostart is an elevated Task Scheduler task with `InteractiveToken`, `HighestAvailable`, and a logon trigger.
- The UI format stays `CPU: 23% | 58 °C`, `RAM: 8.2 / 31.9 GB (26%)`; README is English and internal docs are Russian.
- `--diagnostics`, `--ui-check`, and `--autostart-check` keep their current output keys and bypass the single-instance mutex.
