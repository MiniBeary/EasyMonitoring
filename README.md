# EasyMonitoring

Простой tray-монитор для Windows: анимированная иконка в трее, скорость которой зависит от загрузки CPU, плюс CPU/GPU/RAM в подсказке и контекстном меню.

## Возможности

- 5-кадровая вращающаяся дуга в системном трее.
- Скорость анимации зависит от средней загрузки CPU (2–20 FPS).
- При наведении: `CPU: 23% | 58 °C`.
- По правому клику:
  - `CPU: 23% | 58 °C`
  - `GPU: 5% | 47 °C`
  - `RAM: 8.2 / 31.9 GB (26%)`
  - `Start with Windows` (галочка автозапуска)
  - `Exit`
- Контекстное меню: тёмно-серый фон, белый текст и белая галочка.
- Пункт `Start with Windows` создаёт задачу Task Scheduler — при входе в Windows приложение запускается без UAC-запроса.
- CPU load обновляется раз в секунду (среднее по 3 замерам); температура CPU/GPU, GPU load и RAM — раз в 2 секунды.
- Один экземпляр приложения.
- Работает только в трее, без главного окна.

## Требования

- Windows 10/11 x64.
- Права администратора (приложение запускается с UAC).
- **PawnIO** — драйвер, через который LibreHardwareMonitor читает температуру CPU:
  ```powershell
  winget install --id namazso.PawnIO -e
  ```
  Без PawnIO загрузка CPU, GPU и RAM работают, а температура CPU показывается как `N/A`.
- .NET SDK 9 нужен только для сборки. Опубликованный exe — self-contained и не требует установленного .NET Runtime.

## Сборка и тесты

```powershell
dotnet test EasyMonitoring.sln -c Release
dotnet build EasyMonitoring.sln -c Release
```

## Публикация

```powershell
dotnet publish src/EasyMonitoring.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts/publish
```

Готовый файл: `artifacts/publish/EasyMonitoring.exe`.

## Запуск

```powershell
.\artifacts\publish\EasyMonitoring.exe
```

При старте Windows покажет UAC-запрос. После запуска приложение появляется в области уведомлений. Выход — через правый клик → `Exit`.

## Автозапуск

Правый клик → `Start with Windows`. Приложение создаёт задачу планировщика `EasyMonitoring` с `HighestAvailable` и `InteractiveToken`: при следующем входе в Windows оно запустится в трее без UAC-запроса. Снятие галочки удаляет задачу. Если exe переместить, включите галочку заново — задача будет пересоздана с новым путём.

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

## Структура

```
src/EasyMonitoring.Core/   чистая логика
src/EasyMonitoring.App/    WinForms tray-приложение
tests/                     xUnit-тесты Core
docs/                      план и QA-чек-лист
artifacts/publish/         опубликованный exe
```

## Ограничения

- UAC-запрос при каждом запуске — следствие `requireAdministrator`.
- Для температуры CPU требуется PawnIO; без него показывается `N/A`.
- Набор GPU-сенсоров зависит от драйвера и LibreHardwareMonitor.
- `NotifyIcon.Text` может не обновлять уже открытую системную подсказку; проверяется в QA.
- Нет настроек, автозапуска и выбора темы — это осознанный MVP.
