# EasyMonitoring — QA checklist

- Дата автоматической проверки: 2026-10-20
- Машина: Windows 11 build 26200, Ryzen 9 5900X, RTX 3080 Ti
- Папка проекта: `C:\Users\Admin\Documents\CanvasTTY\Monitor`

## Автоматические проверки

- [x] `dotnet test EasyMonitoring.sln -c Release` — 64/64 теста Core проходят.
- [x] `dotnet build EasyMonitoring.sln -c Release` — 0 ошибок, 0 предупреждений.
- [x] `dotnet publish ...` — создан self-contained single-file `artifacts/publish/EasyMonitoring.exe`.
- [x] `--diagnostics` на опубликованном exe — получены CPU load/temp, GPU load/temp, RAM.
- [x] `--ui-check` — проверены `NotifyIcon.Text`, пункты меню, цвета `FF3C3C3C`/`FFFFFFFF`, `ShowCheckMargin`, автозагрузка, интервал анимации, 5 кадров.
- [x] `--autostart-check` — задача создаётся, XML содержит `HighestAvailable`, `InteractiveToken`, `LogonTrigger`, затем удаляется.
- [x] UI читает состояние автозапуска: при временно созданной задаче `--ui-check` выводит `AutostartChecked: True`.
- [x] Второй экземпляр завершается с кодом 0, первый продолжает работать.
- [x] Основной manifest resource exe: `requireAdministrator`, `asInvoker` отсутствует (проверено чтением `RT_MANIFEST`).
- [x] PE exe: `AMD64`, subsystem `GUI` — консольного окна нет.
- [x] Лог ошибок пуст после успешных запусков.
- [x] Выход через пункт меню `Exit` корректно завершает message loop (проверено в `--ui-check`).
- [x] Значения GPU сверены с `nvidia-smi`: температура 47 °C совпадает; load отличается в пределах обычного разброса.
- [x] RAM сверена с `Win32_OperatingSystem`: ~31.9 GB total, 39–40% used.

## Результаты последнего прогона

```text
EasyMonitoring diagnostics
CPU: 2% | 71 °C
GPU: 11% | 46 °C
RAM: 13.2 / 31.9 GB (41%)

EasyMonitoring UI diagnostics
Snapshots: 1
TrayText: CPU: 2% | 70 °C
Menu[0]: CPU: 2% | 70 °C
Menu[1]: GPU: 10% | 46 °C
Menu[2]: RAM: 13.2 / 31.9 GB (41%)
Menu[3]: Start with Windows
Menu[4]: Exit
AnimationIntervalMs: 490
AnimationFrameCount: 5
CurrentFrameIndex: 2
AutostartChecked: False
AutostartStateLoaded: True
MenuBackColor: FF3C3C3C
MenuForeColor: FFFFFFFF
ShowCheckMargin: True

EasyMonitoring autostart diagnostics
Initial: False
Enable: success=True error=none
EnabledState: True
TaskXmlRunLevel: True
TaskXmlInteractiveToken: True
TaskXmlLogonTrigger: True
Disable: success=True error=none
DisabledState: True
```

## Производительность (idle, окно 5 минут, замер каждые 5 с)

| Метрика | EasyMonitoring до оптимизации | EasyMonitoring после | RunCat 365 3.6.0 |
| --- | --- | --- | --- |
| CPU, % одного ядра | 2.91 | 1.61 | 8.18 |
| CPU, % суммарно по 24 потокам | 0.121 | 0.067 | 0.341 |
| Working set, avg / peak | 88.4 / 96.1 MB | 85.1 / 89.9 MB | 80.4 / 80.5 MB |
| Private bytes, avg / peak | 43.0 / 51.0 MB | 39.0 / 43.6 MB | 43.3 / 43.5 MB |
| Handles, avg | 391 | 411 | 621 |
| GDI objects, peak | 26 | 26 | 36 |
| Threads, peak | 17 | 18 | 11 |
| Размер поставки | 112 MB | 112 MB | 141 MB (MSIX) |

- [x] После оптимизации CPU снизился с ~2.9% до ~1.6% одного ядра.
- [x] Пик working set снизился с ~96 MB до ~90 MB, private bytes — с ~51 MB до ~44 MB.
- [x] Добавление темы меню и автозапуска не дало заметной регрессии CPU/памяти.
- [x] EasyMonitoring потребляет примерно в 5 раз меньше CPU, чем RunCat 365 в простое.
- [x] GDI/handles у EasyMonitoring ниже, чем у RunCat.
- [x] I/O-активность за окно замера: 0 байт у обоих.

Методика: оба процесса запущены одновременно; Windows в простое; из `Get-Process` снимались `TotalProcessorTime`, working set и private bytes, из `GetGuiResources` — GDI/USER, с интервалом 5 с; CPU считался как разница процессорного времени за окно, в процентах одного ядра. RunCat работал с 17.10, EasyMonitoring перезапускался за ~15 с до замера, чтобы исключить влияние старта.

Оптимизация: ссылки на hardware/сенсоры LHM кэшируются один раз при открытии; LHM (температура CPU/GPU, GPU load) и RAM опрашиваются раз в 2 с; CPU load и анимация — раз в 1 с. Замер «после» сделан уже с тёмной темой меню и фоновым опросом автозапуска.

## Ручные проверки

- [ ] Иконка EasyMonitoring видна в области уведомлений (или в overflow-меню).
- [ ] При наведении показывается `CPU: …% | … °C`.
- [ ] Подсказка обновляется при изменении нагрузки.
- [ ] По правому клику видны CPU, GPU, RAM, `Start with Windows` и Exit.
- [x] Фон меню тёмно-серый, весь текст белый; disabled-строки тоже белые. Проверено по `docs/assets/menu.png`.
- [x] Галочка `Start with Windows` видна и читается. Проверено по `docs/assets/menu.png`.
- [ ] Включение/выключение галочки создаёт/удаляет задачу `EasyMonitoring`.
- [ ] После перезагрузки/входа в Windows приложение стартует без UAC и появляется в трее.
- [ ] Информационные пункты меню неактивны.
- [ ] Анимация визуально вращается; при нагрузке ускоряется, в покое замедляется.
- [ ] При запуске появляется UAC-запрос.
- [ ] Второй запуск не создаёт вторую иконку.
- [ ] Exit убирает иконку и процесс.
- [ ] Нет консольного окна.
- [ ] DPI 100/125/150%: иконка и меню читаемы.
- [ ] 10-минутный soak: потребление памяти стабильно, процесс не падает.
- [ ] CPU load сверен с Task Manager.
- [ ] CPU temp сверена с HWiNFO или другим независимым датчиком.
- [ ] При отсутствии PawnIO температура CPU показывается как N/A, приложение не падает.

## Примечания

- Во время автоматического прогона панель задач была скрыта/автоскрыта и находилась вне видимой области экрана, поэтому визуальные проверки трея остаются ручными.
- Меню снято через временный preview-режим с настоящим `EasyMonitoringMenuRenderer`: `docs/assets/menu.png`. На скриншоте виден белый disabled-текст, белая галочка и тёмный фон. Preview-код удалён из финальной сборки.
- `--autostart-check` проверяет создание/удаление реальной задачи, но не перезагрузку Windows; старт при входе остаётся ручной проверкой.
- Служба PawnIO не остановилась через `Stop-Service` (защищена), поэтому сценарий N/A при отсутствии драйвера проверен unit-тестами; для ручной проверки потребовалось бы удалить драйвер и установить его заново.
- Репозиторий: `https://github.com/MiniBeary/EasyMonitoring`; issues включены. Результаты QA ведутся в этом файле.

## GitHub и релиз

- [ ] `build.yml` проходит на GitHub Actions для `main`.
- [ ] README и `docs/assets/menu.png` корректно отображаются на GitHub.
- [ ] Release `v1.0.0` создан как pre-release.
- [ ] В релизе есть `EasyMonitoring-win-x64.exe` и `SHA256SUMS.txt`.
- [ ] SHA256 скачанного exe совпадает с `SHA256SUMS.txt`.
- [ ] В репозитории нет `artifacts/`, `bin/`, `obj/` и опубликованного exe.
