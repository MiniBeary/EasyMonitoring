# EasyMonitoring — QA checklist

## Исправления 2026-10-25 — текущая ветка, ещё не релиз

- Результаты от 2026-10-20 ниже — исторические; новых замеров производительности нет.
- Добавлены тесты presence/ownership/protection, миграции собственной небезопасной задачи и запрета изменений чужой/неопределённой задачи.
- Проверяются HRESULT, таймауты, XML/действия/принципал и защищённый staging XML с блокировкой от записи и удалением.
- Проверяются синтетические ACL и реальные защищённые Windows-пути, reparse points и SUBST.
- Реальная задача с уникальным именем запускает отдельную marker-программу и удаляется; штатная задача пользователя не используется.
- Task Scheduler зарегистрировал, но не выполнил GUID Exec (0x80070057). Handles/GUID обеспечивают проверку идентичности, а команда использует подтверждённую букву физического тома Mount Manager.
- Sampler проверяется с заблокированным baseline и следующим чтением: после таймаута источник остаётся жив до завершения чтения, позднего snapshot нет.
- `--ui-check` не ремонтирует задачу пользователя; `--autostart-check` использует уникальную diagnostic-задачу и cleanup в finally.
- Администраторские integration-тесты явно пропускаются без повышения прав.

### Итоговая проверка 2026-10-29

- [x] `dotnet build EasyMonitoring.sln -c Release --nologo` — 0 ошибок, 0 предупреждений.
- [x] `dotnet test EasyMonitoring.sln -c Release --no-build --nologo` — 73 Core + 48 App = **121/121**, пропусков нет; реальные Windows integration-тесты выполнены с повышением прав.
- [x] `dotnet publish src/EasyMonitoring.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/publish` — успешно.
- [x] Опубликованный EXE: `--diagnostics` и `--ui-check` — exit 0.
- [x] Опубликованный EXE из пользовательской папки: `--autostart-check` — ожидаемый exit 1, регистрация отклонена, `Cleanup: removed=True`.
- [x] Временная защищённая копия опубликованного EXE: `--autostart-check` — exit 0; регистрация, XML principal/trigger, фактическое включение и удаление подтверждены.
- [x] В Windows integration-тесте зарегистрированная задача действительно выполнила marker-программу из защищённого пути; проверялся путь с пробелами, кириллицей и `&`.
- [x] После диагностики XML/код запроса штатной задачи `EasyMonitoring` не изменились. Изолированные задачи и временные защищённые папки удалены.
- [x] Независимый разбор обнаружил ошибку классификации непроверяемого пути как Unsafe. Добавлен воспроизводящий тест через guard → adapter → Core; после исправления такие пути дают Unknown и не запускают миграционное удаление.
- [x] Первый CI-прогон обнаружил потерю кириллицы в выводе `schtasks`. Контроль с CP437 воспроизвёл `????`; XML-запрос теперь использует отдельную скрытую UTF-8 консоль. Проверены сохранность кодовой страницы родителя, правильный HRESULT и отказ shell-символам в аргументах запроса.
- [x] Проверены локальные ссылки README, наличие скриншота, блоки команд и актуальный релиз: `v1.0.1` от 2026-10-20. Исправления этой ветки ещё не опубликованы.
- Новых сравнительных замеров производительности нет. Видимость иконки, DPI и запуск после настоящего входа в Windows остаются ручными проверками; hidden UI diagnostics их не подтверждают.

## Исторический прогон

- Дата автоматической проверки: 2026-10-20
- Машина: Windows 11 build 26200, Ryzen 9 5900X, RTX 3080 Ti
- Папка проекта: `C:\Users\Admin\Documents\CanvasTTY\Monitor`

## Автоматические проверки

- [x] `dotnet test EasyMonitoring.sln -c Release` — 62 Core + 2 App = 64/64 теста проходят.
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
CPU: 7% | 75 °C
GPU: 7% | 47 °C
RAM: 16.7 / 31.9 GB (52%)

EasyMonitoring UI diagnostics
Snapshots: 1
TrayText: CPU: 7% | 74 °C
Menu[0]: CPU: 7% | 74 °C
Menu[1]: GPU: 10% | 46 °C
Menu[2]: RAM: 16.7 / 31.9 GB (52%)
Menu[3]: Start with Windows
Menu[4]: Exit
AnimationIntervalMs: 470
AnimationFrameCount: 5
CurrentFrameIndex: 3
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

После архитектурного рефакторинга выполнен короткий idle-замер опубликованного exe (60 с, интервал 5 с): **1.74% одного ядра**, working set **84.1 MB avg / 86.2 MB peak**, handles **422**, threads **14**. Полный 5-минутный сравнительный замер выше не перезапускался; cadence 1 s/2 s сохранён.

## Архитектурный рефакторинг

- [x] Metrics: `MetricsSnapshotModule` владеет cadence/freshness/средними; GPU-среднее больше не повторяет cached samples; добавлены тесты первого тика, cadence, hold-one, recovery и source exceptions.
- [x] Autostart: `AutostartStateModule` владеет actual/desired/unknown; unknown остаётся retryable; toggle перечитывает фактическое состояние; QA-ключи сохранены.
- [x] Sensors: `SensorResolver` выбирает по стабильным `SensorId`; LHM-adapter связывает один раз; CPU fallback больше не берёт посторонний temperature-сенсор.
- [x] Tray: `TrayPresentationState` в Core, `TrayPresentation` — тонкий WinForms-adapter, `TrayApplicationContext` — host/lifecycle; `ui-diagnostics.txt` сохранил ключи.
- [x] Добавлен `tests/EasyMonitoring.App.Tests`; App.Tests проверяет snapshot/diagnostics и unknown autostart display.
- [x] `--diagnostics`, `--ui-check`, `--autostart-check` на published exe завершаются с кодом 0; лог пуст.

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

- [x] `build.yml` проходит на GitHub Actions для `main`: run `39346229450`, success.
- [x] README и `docs/assets/menu.png` отдаются GitHub: HTTP 200, изображение 201×150.
- [x] Release `v1.0.0` создан как pre-release: https://github.com/MiniBeary/EasyMonitoring/releases/tag/v1.0.0
- [x] В релизе есть `EasyMonitoring-win-x64.exe` (117 456 639 байт) и `SHA256SUMS.txt`.
- [x] SHA256 скачанного exe совпадает: `6924f872ae29dd700a868b93c72b748519658543c1e922a789e27a459b1ad38a`.
- [x] Скачанный release-exe проходит `--ui-check` с кодом 0; логи пусты.
- [x] В репозитории нет `artifacts/`, `bin/`, `obj/` и опубликованного exe.
- [x] Репозиторий публичный, default branch `main`, MIT определяется GitHub, issues включены, wiki/discussions выключены.
- [x] Добавлены topics: `windows`, `dotnet`, `winforms`, `tray`, `system-monitor`, `cpu`, `gpu`, `ram`, `temperature`, `librehardwaremonitor`, `desktop-app`.
- [x] Dependabot создал PR для Actions и тестовых NuGet-зависимостей; обновления Actions и зелёные test-only PR применены.

## Подготовка v1.0.1

- [x] Версия exe: `1.0.1.0` / `1.0.1`.
- [x] `dotnet build` — 0 ошибок, 0 предупреждений; `dotnet test` — 62 Core + 2 App = 64/64.
- [x] Published exe: `--diagnostics`, `--ui-check`, `--autostart-check` — exit 0, лог пуст.
- [x] Локальный SHA256 published exe: `2b5cadfcd401342b86d7e4e5ae48c6b8368dbe159fb126ca49bf5321557ef5a8`.
- [x] Release workflow переведён на стабильный GitHub release (без `--prerelease`).

## Релиз v1.0.1

- [x] Release `v1.0.1` создан как stable/Latest: https://github.com/MiniBeary/EasyMonitoring/releases/tag/v1.0.1
- [x] В релизе есть `EasyMonitoring-win-x64.exe` (117 483 775 байт) и `SHA256SUMS.txt`.
- [x] SHA256 скачанного exe совпадает: `ef40533b476da209a3ae5f6f44ab402e7ed557496dd008f107fe940cadf93597`.
- [x] Скачанный release-exe: version `1.0.1.0` / `1.0.1`, `--ui-check` exit 0, лог пуст.
