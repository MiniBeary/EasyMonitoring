# EasyMonitoring

**Лёгкий tray-монитор для Windows: анимированная иконка, CPU/GPU/RAM и автозапуск без UAC-запроса.**

[![Build](https://github.com/MiniBeary/EasyMonitoring/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/MiniBeary/EasyMonitoring/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/MiniBeary/EasyMonitoring?include_prereleases&label=release&style=flat-square)](https://github.com/MiniBeary/EasyMonitoring/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)
[![Windows 10/11 x64](https://img.shields.io/badge/Windows-10%2F11%20x64-0078D4?style=flat-square&logo=windows)](https://github.com/MiniBeary/EasyMonitoring/releases)
[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)

<p align="center">
  <img src="docs/assets/menu.png" alt="Контекстное меню EasyMonitoring" width="260">
</p>

EasyMonitoring живёт только в системном трее и не открывает главное окно. Скорость вращающейся дуги зависит от загрузки CPU, а по правому клику видны текущие значения CPU, GPU и RAM.

> **Статус:** `v1.0.0` — первый публичный pre-release. Приложение не подписано; Windows SmartScreen может показать предупреждение.

## Скачать

1. Установите драйвер **PawnIO** (нужен для температуры CPU):
   ```powershell
   winget install --id namazso.PawnIO -e
   ```
2. Скачайте `EasyMonitoring-win-x64.exe` со страницы [Releases](https://github.com/MiniBeary/EasyMonitoring/releases).
3. Запустите exe и подтвердите UAC. Приложение появится в области уведомлений.
4. Выход — правый клик по иконке → `Exit`.

Рядом с exe лежит `SHA256SUMS.txt`. Проверка целостности:

```powershell
(Get-FileHash .\EasyMonitoring-win-x64.exe -Algorithm SHA256).Hash.ToLower()
Get-Content .\SHA256SUMS.txt
```

Хеши должны совпасть. Файл не подписан сертификатом, поэтому SmartScreen может предупредить о запуске — это ожидаемо для открытой сборки.

## Возможности

- 5-кадровая вращающаяся дуга в системном трее.
- Скорость анимации зависит от средней загрузки CPU (2–20 FPS).
- При наведении: `CPU: 23% | 58 °C`.
- По правому клику:
  - `CPU: 23% | 58 °C`
  - `GPU: 5% | 47 °C`
  - `RAM: 8.2 / 31.9 GB (26%)`
  - `Start with Windows` — автозапуск через Task Scheduler;
  - `Exit`.
- Тёмно-серое меню, белый текст (включая disabled-пункты) и белая галочка.
- Один экземпляр приложения, без главного окна.
- Нет сети и телеметрии: все измерения выполняются локально.

## Автозапуск

Правый клик → `Start with Windows`.

Приложение создаёт задачу планировщика `EasyMonitoring` с `HighestAvailable` и `InteractiveToken`. При следующем входе в Windows оно запускается в трее без UAC-запроса. Снятие галочки удаляет задачу. Если exe переместить, включите галочку заново — задача будет пересоздана с новым путём.

## Требования

- Windows 10/11 x64.
- Права администратора: приложение запускается с UAC из-за доступа к сенсорам.
- **PawnIO** — драйвер, через который LibreHardwareMonitor читает температуру CPU:
  ```powershell
  winget install --id namazso.PawnIO -e
  ```
  Без PawnIO загрузка CPU, GPU и RAM работают, а температура CPU показывается как `N/A`.
- .NET SDK 9 нужен только для сборки. Опубликованный exe — self-contained и не требует установленного .NET Runtime.

## Источники метрик

| Метрика | Источник |
| --- | --- |
| CPU load | `GetSystemTimes` (P/Invoke) |
| CPU temperature | LibreHardwareMonitor 0.9.6 + PawnIO |
| GPU load/temperature | LibreHardwareMonitor, GPU Core |
| RAM | `GlobalMemoryStatusEx` |

CPU load не использует `PerformanceCounter`, потому что на локализованной Windows имена счётчиков могут не совпадать.

## Ресурсы

Замеры в простое на этой машине (Windows 11, Ryzen 9 5900X, Release-сборка, окно 5 минут, средние значения):

| Метрика | EasyMonitoring | RunCat 365 3.6.0 |
| --- | --- | --- |
| CPU, % одного ядра | ~1.6 | ~8.2 |
| CPU, % суммарно по 24 потокам | ~0.066 | ~0.34 |
| Working set | ~85 MB | ~80 MB |
| Private bytes | ~39 MB | ~43 MB |
| Handles | ~411 | ~621 |
| GDI objects | 26 | 36 |
| Threads (peak) | 18 | 11 |
| Размер поставки | 112 MB (один exe) | 141 MB (MSIX) |

RunCat 365 опрашивает больше типов сенсоров и имеет настройки; цифры при нагрузке и на другой машине будут отличаться.

## Диагностика

Для QA предусмотрены три режима, которые не показывают трей и завершаются сами:

```powershell
# Проверка значений CPU/GPU/RAM
.\artifacts\publish\EasyMonitoring.exe --diagnostics

# Проверка объектов tray UI: текст, пункты меню, цвета, кадры анимации
.\artifacts\publish\EasyMonitoring.exe --ui-check

# Проверка автозапуска: включает, проверяет XML задачи и выключает её
.\artifacts\publish\EasyMonitoring.exe --autostart-check
```

Результаты:

- `%LOCALAPPDATA%\EasyMonitoring\diagnostics.txt`
- `%LOCALAPPDATA%\EasyMonitoring\ui-diagnostics.txt`
- `%LOCALAPPDATA%\EasyMonitoring\autostart-diagnostics.txt`

`--autostart-check` временно создаёт реальную задачу `EasyMonitoring` и удаляет её в конце; остальные режимы ничего не меняют.

Лог ошибок: `%LOCALAPPDATA%\EasyMonitoring\EasyMonitoring.log`.

## Сборка и тесты

```powershell
dotnet test EasyMonitoring.sln -c Release
dotnet build EasyMonitoring.sln -c Release
```

## Публикация

Остановите запущенный `EasyMonitoring.exe`, затем:

```powershell
dotnet publish src/EasyMonitoring.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o artifacts/publish
```

Готовый файл: `artifacts/publish/EasyMonitoring.exe`.

## Архитектура

- `MetricsSnapshotModule` в Core владеет cadence, свежестью, средними и политикой пропусков; App-слой только опрашивает источники.
- `AutostartStateModule` владеет actual/desired/unknown состоянием Task Scheduler; Windows-adapter отвечает за `schtasks` и XML.
- `SensorResolver` выбирает сенсоры по стабильным `SensorId`; LHM-adapter связывает их с живыми сенсорами один раз.
- `TrayApplicationContext` — host и lifecycle; `TrayPresentationState` — отображаемое состояние; `TrayPresentation` — тонкий WinForms-адаптер.
- Подробный словарь терминов — в [CONTEXT.md](CONTEXT.md).

## Структура

```
src/EasyMonitoring.Core/   чистая логика
src/EasyMonitoring.App/    Windows/LHM-адаптеры и tray UI
tests/                     xUnit-тесты Core и App
docs/                      план, QA-чек-лист и заметки
artifacts/publish/         опубликованный exe (не коммитится)
```

## Ограничения

- UAC-запрос при каждом ручном запуске — следствие `requireAdministrator`. Автозапуск через Task Scheduler запускает задачу elevated без отдельного UAC-запроса.
- Для температуры CPU требуется PawnIO; без него показывается `N/A`.
- Набор GPU-сенсоров зависит от драйвера и LibreHardwareMonitor.
- `NotifyIcon.Text` может не обновлять уже открытую системную подсказку; проверяется в QA.
- Нет пользовательских настроек и выбора темы; автозапуск и тёмное меню уже есть.
- exe не подписан; SmartScreen может предупредить.

## Лицензия

Код проекта распространяется под [MIT](LICENSE).

EasyMonitoring использует LibreHardwareMonitorLib 0.9.6 (MPL-2.0) и self-contained .NET 9. Подробности — в [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

**English:** EasyMonitoring is a lightweight Windows tray monitor for CPU, GPU and RAM with a cat-style animation, a dark context menu, and optional elevated autostart through Task Scheduler.
