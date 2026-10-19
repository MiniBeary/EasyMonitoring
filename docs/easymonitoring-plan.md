# EasyMonitoring — план реализации

- Дата: 2026-10-19
- Статус: реализовано; автоматические проверки пройдены, ручные визуальные проверки — в `docs/qa-checklist.md`
- Проект: `EasyMonitoring`
- Рабочий каталог: `C:\Users\Admin\Documents\CanvasTTY\Monitor`

## 1. Цель

Сделать простой Windows tray-монитор, аналогичный RunCat 365, но без лишних функций:

- анимированная иконка в трее, скорость которой зависит от загрузки CPU;
- при наведении мыши — загрузка CPU и температура CPU;
- при правом клике — загрузка GPU, температура GPU, занятая/общая ОЗУ, загрузка CPU и температура CPU;
- пункт Exit для завершения приложения.

Приложение работает только в трее, без главного окна.

## 2. Что входит в MVP

| Возможность | Решение |
| --- | --- |
| Стек | C# WinForms, .NET 9 |
| Платформа | Windows 10/11 x64 |
| CPU load | `GetSystemTimes` (P/Invoke), без `PerformanceCounter` |
| CPU/GPU temperature | `LibreHardwareMonitorLib` 0.9.6 |
| GPU | Любой GPU через LHM: NVIDIA → AMD → Intel; иначе N/A |
| RAM | `GlobalMemoryStatusEx` |
| Иконка | 5 кадров, вращающаяся дуга |
| Обновление | 1 с, среднее по 3 замерам |
| Анимация | 2–20 FPS, интервал 500–50 мс |
| Hover | `CPU: 23% \| 58 °C` |
| ПКМ-меню | CPU, GPU, RAM, Exit |
| Язык | Английский, °C, инвариантное форматирование чисел |
| Запуск | `requireAdministrator` (UAC при каждом старте) |
| Артефакт | self-contained single-file `EasyMonitoring.exe` (win-x64) |
| Тесты | TDD на Core-швах, UI — ручной QA |

## 3. Что не входит в MVP

- настройки и их сохранение;
- автозапуск с Windows;
- выбор темы;
- выбор FPS limit;
- кастомные раннеры;
- мини-игра;
- графики и история метрик;
- сеть, диски, температура прочих компонентов;
- установщик;
- поддержка ARM64 и x86.

## 4. Факты об окружении

- Windows 11 Pro, build 26200.
- CPU: AMD Ryzen 9 5900X (12 ядер / 24 потока).
- GPU: NVIDIA GeForce RTX 3080 Ti.
- `nvidia-smi` работает, GPU temp/util доступны.
- `Get-Counter` на этой системе использует локализованные имена счётчиков; `Processor Information`/`Processor` через `PerformanceCounter` ненадёжны. Поэтому CPU load берём через `GetSystemTimes`.
- ACPI/WMI thermal zone показывает 29 °C и не является температурой пакета CPU. Для CPU temp нужен LHM.
- HWiNFO 8.40 установлен, но не запущен; в MVP не используется.
- .NET SDK не установлен, есть только runtime. Нужен SDK 9.
- Python/Node есть, но выбран C# WinForms.
- HWiNFO/LibreHardwareMonitor не должны быть обязательным внешним процессом: LHM используется как NuGet-библиотека.

## 5. Архитектура

```
EasyMonitoring.sln
  src/EasyMonitoring.Core/                net9.0
    Metrics/
      CpuTimes.cs
      CpuLoadCalculator.cs
      MovingAverage.cs
      MetricsAggregator.cs
      MetricsSnapshot.cs
    Animation/
      AnimationIntervalCalculator.cs
    Formatting/
      StatusFormatter.cs
    Sensors/
      SensorDescriptor.cs
      SensorSelector.cs
  src/EasyMonitoring.App/                 net9.0-windows, WinForms
    Program.cs
    TrayApplicationContext.cs
    LhmMetricsSource.cs
    Native/
      SystemTimes.cs
      GlobalMemoryStatusEx.cs
    Icons/
      ArcIconFactory.cs
    Logging/
      AppLog.cs
    app.manifest
  tests/EasyMonitoring.Core.Tests/        net9.0, xUnit
  docs/qa-checklist.md
  README.md
```

### 5.1. Core

Core не зависит от WinForms и LHM. Содержит чистую логику, которую можно тестировать без администратора и без реального железа.

Ответственности:

- `CpuLoadCalculator` — расчёт загрузки CPU по двум снимкам `GetSystemTimes`.
- `MovingAverage` — скользящее среднее по 3 замерам.
- `MetricsAggregator` — объединение CPU/GPU/RAM и сглаживание.
- `AnimationIntervalCalculator` — CPU load → интервал анимации.
- `StatusFormatter` — строки для tooltip и пунктов меню.
- `SensorSelector` — выбор нужных сенсоров CPU/GPU по именам и типам.

### 5.2. App

- `Program` — single-instance mutex, `ApplicationConfiguration.Initialize()`, запуск `TrayApplicationContext`.
- `TrayApplicationContext` — `NotifyIcon`, `ContextMenuStrip`, таймер анимации, приём снимков метрик, Exit.
- `LhmMetricsSource` — открывает `Computer` с `IsCpuEnabled` и `IsGpuEnabled`, обновляет сенсоры, возвращает `MetricsSnapshot`.
- `SystemTimes` — P/Invoke `GetSystemTimes`.
- `GlobalMemoryStatusEx` — P/Invoke для ОЗУ.
- `ArcIconFactory` — генерация 5 кадров вращающейся дуги и упаковка PNG в ICO.
- `AppLog` — минимальный лог ошибок в `%LOCALAPPDATA%\EasyMonitoring\EasyMonitoring.log`.

### 5.3. Поток данных

```
PeriodicTimer (1 s, background)
  -> GetSystemTimes -> CPU load
  -> LHM Update -> CPU temp, GPU load/temp
  -> GlobalMemoryStatusEx -> RAM
  -> MetricsAggregator (3-sample average)
  -> SynchronizationContext.Post -> UI
       -> NotifyIcon.Text
       -> ContextMenuStrip item texts
       -> animationTimer.Interval = clamp(500 - 4.5 * cpuLoad, 50, 500)
animationTimer.Tick
  -> NotifyIcon.Icon = frames[current]
  -> current = (current + 1) % 5
```

## 6. Детали реализации

### 6.1. CPU load

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
```

- Первый вызов задаёт baseline; первый снимок нагрузки появляется через 1 с.
- `kernel` включает idle, поэтому:
  - `total = kernelDelta + userDelta`;
  - `busy = total - idleDelta`;
  - `load = total > 0 ? busy / total * 100 : 0`.
- Значение clamp в 0..100.
- Если дельта отрицательная (аномалия), вернуть `0` и залогировать.

### 6.2. CPU/GPU temperature и GPU load

- `Computer` из LHM открывается один раз при старте.
- Для температуры CPU требуется установленный драйвер **PawnIO** 2.2.0: NuGet-пакет LHM его не устанавливает. Установка: `winget install --id namazso.PawnIO -e`; после установки перезапустить приложение.
- `IsCpuEnabled = true`, `IsGpuEnabled = true`.
- Остальные категории отключены.
- Обновление сенсоров — раз в секунду в фоновом цикле.
- CPU temp:
  - приоритет имён: `Core (Tctl/Tdie)`, `CPU Package`, `Core Average`, `Core Max`, `Tdie`, `Package`;
  - fallback: максимум среди temperature-сенсоров с `Core`/`CCD` в имени;
  - иначе `null` → N/A.
- GPU:
  - hardware types: `GpuNvidia`, `GpuAmd`, `GpuIntel`;
  - приоритет: NVIDIA → AMD → Intel;
  - load: `SensorType.Load`, предпочтительно `GPU Core`;
  - temp: `SensorType.Temperature`, предпочтительно `GPU Core`;
  - выбираем первый GPU, у которого есть хотя бы один из нужных сенсоров; отсутствующие поля → N/A.
- Любая ошибка LHM логируется; приложение продолжает работать.
- Если LHM вообще не смог открыться, CPU load и RAM продолжают работать, остальное — N/A.

### 6.3. RAM

- `GlobalMemoryStatusEx`.
- `total = ullTotalPhys`, `available = ullAvailPhys`, `used = total - available`.
- `percent = dwMemoryLoad` (или вычисленный, если нужно).
- Формат: `RAM: 8.2 / 31.9 GB (26%)`.

### 6.4. Сглаживание

- Храним последние 3 значения CPU load и GPU load.
- Среднее считается по доступным значениям.
- CPU load всегда доступен; GPU load может быть null.
- Температуры и RAM показываем последними значениями без усреднения.
- Анимация использует средний CPU load.

### 6.5. Animation interval

```text
intervalMs = clamp(round(500 - 4.5 * cpuLoadPercent), 50, 500)
```

Примеры:

| CPU load | Interval | FPS |
| --- | --- | --- |
| 0% | 500 ms | 2 |
| 25% | 388 ms | ~2.6 |
| 50% | 275 ms | ~3.6 |
| 75% | 163 ms | ~6.1 |
| 100% | 50 ms | 20 |

Примечание: `System.Windows.Forms.Timer` не является высокоточным; 50 мс — целевое значение, а не гарантия.

### 6.6. Иконка

- 5 кадров, вращающаяся дуга.
- Размер 32×32, прозрачный фон, контрастный цвет (например, оранжевый) с тёмным контуром.
- Кадры создаются один раз при старте и переиспользуются.
- `NotifyIcon.Icon` меняется на каждом тике таймера.
- Иконки и Bitmap освобождаются при выходе.

### 6.7. Tray UI

- `NotifyIcon.Visible = true`.
- `NotifyIcon.Text` = `CPU: 23% | 58 °C` (обновляется раз в секунду).
- `ContextMenuStrip`:
  - `CPU: 23% | 58 °C` (disabled);
  - `GPU: 5% | 47 °C` (disabled);
  - `RAM: 8.2 / 31.9 GB (26%)` (disabled);
  - separator;
  - `Exit` (enabled).
- Меню обновляется раз в секунду; если открыто — текст обновляется на месте.
- Левый клик ничего не делает.
- Двойной клик ничего не делает.

### 6.8. Single instance

- Named mutex: `Local\EasyMonitoring.SingleInstance`.
- Второй экземпляр сразу завершается.
- Mutex освобождается при выходе.

### 6.9. Manifest

- `requestedExecutionLevel level="requireAdministrator" uiAccess="false"`.
- `dpiAware` / PerMonitorV2.
- Поддержка Windows 10/11.

### 6.10. Логирование

- Файл: `%LOCALAPPDATA%\EasyMonitoring\EasyMonitoring.log`.
- Пишем только ошибки LHM, исключения сэмплера и необработанные исключения.
- Ротация не требуется для MVP.

### 6.11. Exit

- Остановить `animationTimer`.
- Отменить `CancellationTokenSource`.
- Дождаться завершения фонового сэмплера (с коротким таймаутом).
- `Computer.Close()`, `NotifyIcon.Visible = false`, `Dispose()`.
- `ExitThread()`.

## 7. TDD-швы и тесты

Пишем тесты до реализации, по одному срезу за цикл (red → green). UI-тесты не автоматизируем.

### 7.1. `AnimationIntervalCalculator`

- 0% → 500.
- 50% → 275.
- 100% → 50.
- < 0% и > 100% clamp.
- Проверка округления на промежуточных значениях.

### 7.2. `StatusFormatter`

- Tooltip: `CPU: 23% | 58 °C`.
- Tooltip без температуры: `CPU: 23% | N/A`.
- Menu CPU line.
- Menu GPU line с load/temp и с N/A.
- RAM: `RAM: 8.2 / 31.9 GB (26%)`.
- RAM N/A.
- Инвариантная культура: точка как десятичный разделитель.

### 7.3. `CpuLoadCalculator`

- Нет предыдущего снимка → `null`/`0` (по контракту).
- 0% idle.
- 100% busy.
- Смешанная нагрузка.
- Учёт того, что kernel включает idle.
- Отрицательная дельта → 0 + ошибка/лог.

### 7.4. `MovingAverage`

- Один замер → он же.
- Два замера → среднее.
- Три замера → среднее.
- Четвёртый вытесняет первый.
- Null-значения не ломают среднее.

### 7.5. `MetricsAggregator`

- Объединение CPU/GPU/RAM.
- Среднее CPU/GPU load по 3 замерам.
- Последние температуры.
- Отсутствующий GPU → N/A.
- Отсутствующая температура → N/A.
- Отсутствующий GPU load не влияет на CPU.

### 7.6. `SensorSelector`

- CPU temp: приоритет `Core (Tctl/Tdie)`.
- CPU temp: fallback `CPU Package`.
- CPU temp: fallback max core.
- GPU: NVIDIA выбирается раньше AMD/Intel.
- GPU: `GPU Core` load/temp.
- GPU без сенсоров → null.
- GPU только с load → temp null.

### 7.7. Ручные проверки UI

- Hover и ПКМ-меню.
- Анимация и её скорость.
- UAC и elevated-процесс.
- Single instance.
- Exit.
- N/A при недоступных сенсорах.
- DPI 100/125/150%.
- 10-минутный soak.

## 8. Шаги реализации

1. Установить .NET SDK 9 через winget (процесс уже elevated).
2. Проверить `dotnet --list-sdks`.
3. Создать solution и проекты:
   - `src/EasyMonitoring.Core`;
   - `src/EasyMonitoring.App`;
   - `tests/EasyMonitoring.Core.Tests`.
4. Добавить `LibreHardwareMonitorLib 0.9.6` в App.
5. Добавить xUnit в тестовый проект.
6. TDD-цикл по Core-швам: тест → реализация → `dotnet test`.
7. Реализовать App:
   - P/Invoke;
   - LHM-адаптер;
   - иконки;
   - tray-контекст;
   - меню;
   - single instance;
   - manifest;
   - лог.
8. Собрать и запустить вручную; проверить значения.
9. Опубликовать self-contained single-file exe.
10. Прогнать QA-чек-лист.
11. Исправить найденные дефекты через тот же TDD-цикл.
12. Обновить README и итоговый отчёт.

## 9. Команды

```powershell
# SDK
winget install --id Microsoft.DotNet.SDK.9 -e --accept-package-agreements --accept-source-agreements
dotnet --list-sdks

# Solution
dotnet new sln -n EasyMonitoring
dotnet new classlib -n EasyMonitoring.Core -o src/EasyMonitoring.Core -f net9.0
dotnet new winforms -n EasyMonitoring.App -o src/EasyMonitoring.App -f net9.0-windows
dotnet new xunit -n EasyMonitoring.Core.Tests -o tests/EasyMonitoring.Core.Tests -f net9.0

# Packages
dotnet add src/EasyMonitoring.App package LibreHardwareMonitorLib --version 0.9.6

# Test / build
dotnet test EasyMonitoring.sln -c Release
dotnet build EasyMonitoring.sln -c Release

# Publish
dotnet publish src/EasyMonitoring.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts/publish
```

## 10. QA-чек-лист

- [ ] Hover показывает CPU % и CPU temp.
- [ ] Hover обновляется не реже раза в секунду.
- [ ] ПКМ показывает CPU %, CPU temp, GPU %, GPU temp, RAM used/total + %, Exit.
- [ ] Информационные пункты меню неактивны.
- [ ] Exit завершает процесс и убирает иконку.
- [ ] Второй экземпляр не запускается.
- [ ] Нет консольного окна.
- [ ] Приложение запускается с UAC и работает elevated.
- [ ] Анимация: 5 кадров, вращающаяся дуга.
- [ ] При росте CPU load анимация ускоряется; при снижении — замедляется.
- [ ] CPU load совпадает с Task Manager (±несколько %).
- [ ] RAM совпадает с Task Manager.
- [ ] GPU load/temp совпадает с `nvidia-smi`.
- [ ] CPU temp правдоподобна (сверить с HWiNFO/LHM GUI).
- [ ] При недоступном сенсоре показывается N/A, приложение не падает.
- [ ] Ошибки LHM попадают в лог.
- [ ] DPI 100/125/150%: иконка и текст читаемы.
- [ ] 10 минут работы: потребление памяти стабильно.
- [ ] Single-file exe запускается на машине без установленного .NET SDK/runtime.
- [ ] После выхода не остаётся процессов EasyMonitoring.

## 11. Риски и меры

| Риск | Мера |
| --- | --- |
| LHM-драйвер заблокирован политикой Windows | CPU temp N/A, лог, приложение продолжает работать (согласовано) |
| LHM не находит CPU temp на Ryzen 5900X | N/A + лог; при необходимости позже добавить HWiNFO fallback |
| PawnIO не установлен | CPU temp N/A + предупреждение в логе; README содержит команду установки |
| Single-file ломает загрузку нативных компонентов LHM | Перейти на self-contained папку и сообщить |
| `NotifyIcon.Text` не обновляет открытый tooltip | Проверить в QA; при необходимости добавить кастомное hover-окно |
| UAC при каждом запуске | Следствие выбранного `requireAdministrator`; принято |
| Локализованные счётчики Windows | CPU load через `GetSystemTimes`, без PerformanceCounter |
| Разные имена сенсоров LHM | `SensorSelector` с приоритетами и fallback, покрыт тестами |
| Долгий `Computer.Update()` блокирует UI | Сэмплирование в фоне, UI обновляется через `SynchronizationContext` |

## 12. Критерии готовности

1. `dotnet test` — все тесты проходят.
2. `dotnet build` — без ошибок.
3. Опубликован `artifacts/publish/EasyMonitoring.exe`.
4. Все пункты QA-чек-листа выполнены или явно задокументированы как ограничение.
5. README содержит команды сборки, запуска и известные ограничения.
6. Нет незакоммиченных секретов и временных файлов.

## 13. Процессные заметки

- `grilling`: решения по стеку, температуре, GPU, scope, cadence, UI и артефакту зафиксированы в этом документе.
- `tdd`: реализация начинается с red-тестов на Core-швах; UI проверяется вручную.
- `qa`: GitHub-remote у репозитория отсутствует, поэтому issue-файлинг через `gh` не применяется; результаты QA ведутся в `docs/qa-checklist.md`.
- `find-skills`: релевантного готового скилла для WinForms/tray-монитора в каталоге `skills.sh` не найдено; реализация выполняется напрямую.
- Коммиты не создаются без отдельного указания.
