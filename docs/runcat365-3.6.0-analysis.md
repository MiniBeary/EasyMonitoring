# RunCat 365 3.6.0: разбор CPU-пайплайна, трея и настроек

- Дата: 2026-10-19
- Область: измерение загрузки CPU, преобразование нагрузки в скорость анимации, отрисовка иконки в трее, таймеры, настройки и автозапуск.
- Метод: статический разбор установленной сборки через REA 6.7.0 и сверка с открытыми исходниками тега 3.6.0.
- Мы не запускали приложение, не подключались к процессу и не модифицировали его. Анализ только статический.

## Кратко

RunCat 365 3.6.0 - это WinForms-приложение на .NET 9, работающее как tray-приложение через `ApplicationContext` и `NotifyIcon`.

Ключевая логика:

1. Загрузка CPU читается через `System.Diagnostics.PerformanceCounter` из категории `Processor Information`, экземпляр `_Total`.
2. На Windows build >= 26100 используется счетчик `% Processor Time`, на более старых сначала пробуется `% Processor Utility`, затем fallback на `% Processor Time`.
3. Значения снимаются раз в секунду; хранится и усредняется последние 5 замеров.
4. Раз в 5 секунд пересчитывается интервал анимации по формуле `speed = max(1, (load / 5) * fpsRate)`, `intervalMs = (int)(500 / speed)`.
5. Иконка трея - это `System.Windows.Forms.NotifyIcon`; кадры заранее конвертируются в `Icon` и просто подменяются по таймеру. Своего P/Invoke для `Shell_NotifyIcon` в приложении нет.
6. Настройки хранятся через `UserSettings` (WinForms settings), автозапуск в Store-версии идет через MSIX `StartupTask`, в unpackaged-версии - через ключ `HKCU\...\Run`.

## 1. Идентификация цели

| Параметр | Значение |
| --- | --- |
| Store-пакет | `StudioKyome.RunCat` |
| Версия | `3.6.0.0` |
| Архитектура | x64 |
| Тип пакета | MSIX, `runFullTrust`, `EntryPoint=Windows.FullTrustApplication` |
| Основная сборка | `RunCat 365.dll` |
| Размер | 777 728 байт |
| SHA-256 | `b6dfd0a89f1cbaab20fcb1e4cd6ddc88ee8ad2245abe9874fd3558b2322c89df` |
| MVID | `f5f32507-291c-4fac-b14b-85fe8f0e8f4b` |
| Target framework | `.NETCoreApp,Version=v9.0` |
| Runtime | self-contained, `Microsoft.NETCore.App` и `Microsoft.WindowsDesktop.App` 9.0.16 |
| Исходники | тег `3.6.0`, commit `eecfe2d75ce705094f3205b7eccb23be844dde52` |
| Лицензия исходников | Apache-2.0 |

Установленный пакет: `C:\Program Files\WindowsApps\StudioKyome.RunCat_3.6.0.0_x64__nts6hjhfqpj36`.

Примечание: на GitHub у проекта есть тег `3.6.0`, но опубликованные GitHub Releases заканчиваются на `3.5.0`. Store-сборка 3.6.0.0 при этом существует. Поэтому исходники тега использовались для интерпретации поведения, а идентичность проверялась по версии, именам типов/методов и строковым литералам установленной сборки.

## 2. Как проверялось

Использовался REA 6.7.0, провайдер `rea-dotnet-static` (static managed PE/CLI inspection). Код не загружался и не исполнялся.

Основные Evidence-записи:

| Проверка | Evidence ID |
| --- | --- |
| `inspect-managed-artifact` | `ev_069ee819600fa137ec6986e6b796ee8a9927fae5d311cb35c1ad7efb9031cb0a` |
| `inspect-managed-members` | `ev_ac68683b00ea18b7acbcbe21d9d66360e34dc6edbdd859967ce022b0c719ac74` |
| `inspect-managed-native-boundaries` | `ev_34967c5abe2c5fa299528db321ff08c0a410239610bb74388c2d91c91e2cac7f` |

Инвентарь метаданных: 104 типа, 620 методов, 362 поля, 3000 call edges.

Ключевой результат по нативным границам: в основной сборке объявлен ровно один P/Invoke - `GlobalMemoryStatusEx` из `kernel32.dll`. Импортов `Shell_NotifyIcon`, `user32.dll` или `shell32.dll` нет. Это подтверждает, что трей реализован через WinForms `NotifyIcon`, а не через собственный Win32-код.

Сверка с исходниками: имена типов и методов установленной сборки совпадают с исходниками тега 3.6.0, включая `CPUPerformanceCounters.TaskManagerUsesProcessorTime`, `RunCat365ApplicationContext.CalculateInterval`, `FrameAnimationView`, `FPSMaxLimitExtension.GetRate`, `LaunchAtStartupManager` и строковые литералы категорий счетчиков. Побайтовое сравнение IL с независимой сборкой не выполнялось: в окружении нет рабочего .NET SDK/ILSpy, а GitHub Release 3.6.0 отсутствует.

## 3. Архитектура

```
Program.Main
  - mutex "_RUNCAT_MUTEX" (single instance)
  - ApplicationConfiguration.Initialize()
  - Application.SetColorMode(SystemColorMode.System)
  - Application.Run(new RunCat365ApplicationContext())

RunCat365ApplicationContext
  - CPURepository / GPURepository / MemoryRepository
  - TemperatureRepository / StorageRepository / NetworkRepository
  - CustomRunnerRepository
  - LaunchAtStartupManager
  - ContextMenuManager (NotifyIcon + ContextMenuStrip + Icon frames)
  - fetchTimer (1000 ms)
  - animateTimer (200 ms initially, then calculated interval)

fetchTimer.Tick (1 s)
  -> Task.Run: cpuRepository.Update(), gpuRepository.Update()
  -> every 5th tick: memory/storage/network/temperature update
  -> FetchSystemInfo()
       -> ContextMenuManager.SetNotifyIconText(...)
       -> ContextMenuManager.SetSystemInfoMenuText(...)
       -> CalculateInterval(...)
  -> animateTimer.Stop(); animateTimer.Interval = interval; animateTimer.Start()

animateTimer.Tick
  -> ContextMenuManager.AdvanceFrame()
       -> notifyIcon.Icon = icons[current]
       -> current = (current + 1) % icons.Count
```

Источники: `Program.cs:54-145`, `Program.cs:282-381`, `ContextMenuManager.cs:449-458`.

## 4. CPU -> скорость анимации

### 4.1. Чтение загрузки CPU

Используется `PerformanceCounter` (`CPURepository.cs:48-126`):

- Категория: сначала `Processor Information`, экземпляр `_Total`; если не удалось - `Processor`, `_Total`.
- Счетчики:
  - `Total`: `% Processor Utility` или `% Processor Time`;
  - `User`: `% User Time`;
  - `Kernel`: `% Privileged Time`.
- Выбор `Total` зависит от версии Windows (`CPURepository.cs:79-105`):
  - build >= 26100: сразу `% Processor Time` (как Task Manager в Windows 11 24H2+);
  - build < 26100: сначала `% Processor Utility`, при ошибке - `% Processor Time`.
- После создания счетчиков вызывается `NextValue()` для прайминга (`CPURepository.cs:106-108`).
- `Idle` не читается отдельным счетчиком, а вычисляется как `max(0, 100 - user - kernel)` (`CPURepository.cs:148`).
- Значения `total`, `user`, `kernel` ограничиваются сверху 100 (`CPURepository.cs:145-147`).
- `CPURepository.Get()` возвращает среднее по последним 5 замерам (`CPURepository.cs:165-176`).

На установленной машине OS build 26100, поэтому используется `% Processor Time`.

### 4.2. Частота обновления

- `fetchTimer` - `System.Windows.Forms.Timer` с интервалом 1000 ms (`Program.cs:54`, `Program.cs:137-142`).
- Каждый тик обновляет CPU и GPU в `Task.Run`, чтобы не блокировать UI (`Program.cs:346-364`).
- Каждый 5-й тик дополнительно обновляет memory/storage/network/temperature и пересчитывает интервал анимации (`Program.cs:355-371`).
- `fetchCounter` стартует со значения 5, поэтому первый тяжелый проход выполняется уже на первом тике, затем - каждые 5 секунд.
- Флаг `isFetching` защищает от наложения тиков (`Program.cs:348-380`).
- Среднее по 5 замерам используется для расчета скорости; сам интервал анимации меняется раз в 5 секунд, а не каждый кадр.

### 4.3. Формула

`RunCat365ApplicationContext.CalculateInterval` (`Program.cs:301-312`):

```csharp
var load = speedSource switch
{
    SpeedSource.CPU => cpuInfo.Total,
    SpeedSource.GPU => gpuInfo?.Maximum ?? 0f,
    SpeedSource.Memory => memoryInfo.MemoryLoad,
    _ => 0f,
};
var speed = (float)Math.Max(1.0f, (load / 5.0f) * fpsMaxLimit.GetRate());
return (int)(500.0f / speed);
```

Где `FPSMaxLimitExtension.GetRate()` (`FPSMaxLimit.cs:41-51`):

| Настройка | Rate |
| --- | --- |
| FPS40 | 1.0 |
| FPS30 | 0.75 |
| FPS20 | 0.5 |
| FPS10 | 0.25 |

Теоретически для `load > 5%`:

```
fps ~= 0.4 * loadPercent * rate
intervalMs = 1000 / fps
```

При `load` от 0 до 5% срабатывает нижняя граница `speed = 1`, то есть 500 ms на кадр (2 FPS). При 100% и FPS40 - 25 ms (40 FPS). Значение `(int)` отбрасывает дробную часть, поэтому интервал может быть на 1 ms меньше.

Примеры:

| Load | FPS40 | FPS30 | FPS20 | FPS10 |
| --- | --- | --- | --- | --- |
| 0-5% | 500 ms / 2 fps | 500 ms / 2 fps | 500 ms / 2 fps | 500 ms / 2 fps |
| 25% | 100 ms / 10 fps | 133 ms / ~7.5 fps | 200 ms / 5 fps | 400 ms / 2.5 fps |
| 50% | 50 ms / 20 fps | 66 ms / ~15 fps | 100 ms / 10 fps | 200 ms / 5 fps |
| 75% | 33 ms / ~30 fps | 44 ms / ~23 fps | 66 ms / ~15 fps | 133 ms / ~7.5 fps |
| 100% | 25 ms / 40 fps | 33 ms / ~30 fps | 50 ms / 20 fps | 100 ms / 10 fps |

Практическое ограничение: анимация трея управляется `System.Windows.Forms.Timer`, то есть WM_TIMER, а не высокоточным таймером. Интервалы 25-33 ms не гарантируются: фактический тик может быть заметно реже, особенно при занятом UI-потоке. 40 FPS - это целевое значение, а не жесткая гарантия.

### 4.4. Источники скорости

`SpeedSource` (`SpeedSource.cs:20-25`):

- `CPU`: `cpuInfo.Total`, среднее за 5 секунд.
- `GPU`: `gpuInfo.Maximum`, максимум по последним 5 замерам; GPU читается из категории `GPU Engine`, счетчик `Utilization Percentage`, только инстансы с `engtype_3D` (`GPURepository.cs:44-61`). Список инстансов обновляется каждые 30 тиков (`GPURepository.cs:65-88`).
- `Memory`: `memoryInfo.MemoryLoad` из `GlobalMemoryStatusEx` (`MemoryRepository.cs:57-92`).

Если GPU-счетчики недоступны, `ResolveSpeedSource()` переключает источник на CPU (`Program.cs:168-174`). CPU в меню считается доступным всегда (`Program.cs:157-166`), даже если счетчики не создались; в этом случае показывается balloon tip, а `cpuInfo.Total` остается 0, поэтому анимация падает на нижнюю границу 2 FPS (`Program.cs:176-188`).

## 5. Трей

### 5.1. NotifyIcon

`ContextMenuManager` создает `System.Windows.Forms.NotifyIcon` (`ContextMenuManager.cs:23`), назначает `ContextMenuStrip` и делает его видимым (`ContextMenuManager.cs:190-208`). Иконка меняется присваиванием `notifyIcon.Icon = icons[current]` (`ContextMenuManager.cs:449-458`).

REA подтверждает: в сборке нет импорта `Shell_NotifyIcon`; единственный P/Invoke - `GlobalMemoryStatusEx`. То есть весь tray-функционал идет через WinForms.

Тултип:

- `SetNotifyIconText` устанавливает `notifyIcon.Text` (`ContextMenuManager.cs:465-468`).
- Текст обновляется раз в 5 секунд из `FetchSystemInfo`: CPU/GPU/Memory + температура (`Program.cs:287-299`, `Program.cs:323`).
- `NotifyIcon.Text` - обычная строка; Windows ограничивает длину подсказки, поэтому длинный многострочный текст может обрезаться.

### 5.2. Кадры иконки

`SetIcons` (`ContextMenuManager.cs:308-325`):

- Имя ресурса: `{runner}_{i}` в нижнем регистре, например `cat_0`, `cat_1`, ...
- Количество запрашиваемых кадров: `RunnerExtension.GetFrameNumber` (`Runner.cs:50-59`):
  - Cat: 5;
  - Parrot: 10;
  - Horse: 14.
- В `Resources.resx` этого билда реально есть:
  - `cat_0` ... `cat_4` (5 кадров трея);
  - `parrot_0` ... `parrot_9` (10);
  - `horse_0` ... `horse_4` (5).
- Отсутствующие ресурсы просто пропускаются, поэтому у Horse эффективно 5 кадров, несмотря на запрошенные 14.
- `cat_running_0..4` и `cat_jumping_0..9` - это не кадры трея, а спрайты мини-игры (`Cat.cs:23-117`).

### 5.3. Тема и генерация Icon

`ReplaceIconList` (`ContextMenuManager.cs:342-374`):

- Для светлой темы берется исходный Bitmap.
- Для темной темы каждый пиксель перекрашивается в белый с сохранением альфы (`BitmapExtension.Recolor`, `BitmapExtension.cs:42-73`).
- Каждый Bitmap конвертируется в `Icon` вручную: в ICO-контейнер записывается PNG- payload (`BitmapExtension.ToIcon`, `BitmapExtension.cs:75-101`).
- Иконки создаются заново только при смене темы/раннера, а не на каждом кадре. На каждом тике меняется только ссылка `notifyIcon.Icon`.
- `current` сбрасывается в 0; первый кадр сразу назначается, затем `current = 1 % icons.Count` (`ContextMenuManager.cs:359-370`).

Тема:

- Системная тема читается из `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\SystemUsesLightTheme` (`Program.cs:147-155`).
- `0` - Dark, `1` - Light.
- При `SystemEvents.UserPreferenceChanged` категории `General` иконки перекрашиваются/пересоздаются (`Program.cs:190-202`).
- Ручная тема (`System`, `Light`, `Dark`) хранится в настройках; `ResolveTheme` подставляет системную, если выбрана `System` (`ContextMenuManager.cs:383-386`).

### 5.4. Меню

`ContextMenuManager` собирает `ContextMenuStrip` (`ContextMenuManager.cs:31-209`):

- системная информация (CPU user/kernel/idle, GPU average/maximum, memory total/used/available, storage C/D, network sent/received, temperature);
- выбор раннера (Cat/Parrot/Horse) и кастомных раннеров;
- настройки: тема, источник скорости, FPS max limit, единица температуры, автозапуск;
- информация о версии и ссылка на проект;
- Endless Game;
- Exit.

## 6. Настройки, тема, автозапуск

### 6.1. UserSettings

Значения по умолчанию (`Properties/UserSettings.settings:5-29` и `RunCat 365.dll.config`):

| Настройка | По умолчанию |
| --- | --- |
| Runner | `Cat` |
| Theme | пусто (системная) |
| TemperatureUnit | пусто (системная) |
| FPSMaxLimit | `FPS40` |
| FirstLaunch | `True` |
| HighScore | `0` |
| SpeedSource | `CPU` |
| CustomRunnerName | пусто |

Изменения сохраняются через `UserSettings.Default.Save()` (`Program.cs:220-280`).

Фактический путь в установленной MSIX-версии (AppData виртуализируется пакетом):

```
%LOCALAPPDATA%\Packages\StudioKyome.RunCat_nts6hjhfqpj36\LocalCache\Local\Studio_Kyome\
RunCat_365_Url_5ts0132m2dmiobe54hrjx1yedwqha205\3.6.0.0\user.config
```

На момент анализа там сохранены только `FirstLaunch=False` и `TemperatureUnit=Celsius`; остальные значения берутся по умолчанию.

### 6.2. Автозапуск

`LaunchAtStartupManager` (`LaunchAtStartupManager.cs:109-136`) выбирает реализацию:

- Packaged (MSIX): `Windows.ApplicationModel.StartupTask.GetAsync("RunCatStartup")` (`LaunchAtStartupManager.cs:26-71`).
  - `AppxManifest.xml` объявляет `uap5:StartupTask TaskId="RunCatStartup"` (строки 24-26).
  - Состояния `DisabledByUser` и `DisabledByPolicy` дают пользователю понятные сообщения.
- Unpackaged: ключ `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, значение с именем `Application.ProductName` и путем `Environment.ProcessPath` (`LaunchAtStartupManager.cs:73-107`).

Установленная версия - Store/MSIX, поэтому используется `StartupTask`.

### 6.3. Single instance

`Program.Main` создает named mutex `_RUNCAT_MUTEX`; второй экземпляр сразу завершается (`Program.cs:36-37`).

### 6.4. Кастомные раннеры

`CustomRunnerRepository` (`CustomRunnerRepository.cs:22-43`, `94-121`):

- Каталог: `%LOCALAPPDATA%\RunCat365\CustomRunners` (в MSIX виртуализуется в `...\LocalCache\Local\RunCat365\CustomRunners`).
- Профили: `profiles.json`.
- Кадры: `frame_N.png`.
- Ограничения: 2-30 кадров, высота до 32 px, ширина 10-32 px; при необходимости кадр масштабируется HighQualityBicubic.
- Имя каталога санитизируется и приводится к нижнему регистру.

## 7. Что можно переиспользовать

1. Разделять сэмплирование и анимацию. В RunCat CPU читается раз в секунду, а интервал анимации пересчитывается раз в 5 секунд. Это дешевле, чем менять скорость на каждом кадре.
2. Сглаживать нагрузку. Среднее по 5 замерам уменьшает дергание кота.
3. Держать маппинг явным и настраиваемым. Формула `interval = 500 / max(1, load / 5 * rate)` простая и предсказуемая; `rate` задает верхний FPS.
4. Для трея на WinForms использовать `NotifyIcon` + заранее подготовленный список `Icon`. Не создавать иконки на каждом кадре; менять только ссылку.
5. Для темной темы перекрашивать Bitmap с сохранением альфы и конвертировать PNG в ICO вручную. Это дешево и не требует внешних библиотек.
6. Учитывать ограничения `System.Windows.Forms.Timer`: 25-33 ms - это цель, а не гарантия; для точной высокой частоты нужен другой таймер и отдельный поток.
7. Для CPU-счетчиков предусмотреть fallback: разные категории и разные счетчики на разных версиях Windows; на Windows 11 24H2+ Task Manager использует `% Processor Time`.
8. Для Store-приложения использовать MSIX `StartupTask`, а для обычного exe - `HKCU\...\Run`.
9. Предусмотреть single-instance mutex и корректное освобождение счетчиков/иконок в `Dispose`.

## 8. Ограничения анализа

- REA выполнял только статический разбор PE/CLI. Исполнение кода, реальные значения счетчиков и фактическая плавность анимации не наблюдались.
- Поведение восстановлено по исходникам тега 3.6.0. Имена типов/методов и строковые литералы установленной сборки совпадают с исходниками, но побайтовая идентичность IL не проверялась.
- GitHub Releases не содержат 3.6.0; Store-сборка 3.6.0.0 может быть собрана из тега 3.6.0 с отличиями в упаковке.
- Нативный движок (Ghidra/IDA) не использовался: для управляемой сборки он не нужен. `Microsoft.Windows.SDK.NET.dll` и framework-сборки отдельно не разбирались.
- Приложение во время анализа было запущено; мы не подключались к процессу, не снимали дампы и не использовали Frida/отладчик.
- REA 6.7.0 установлен глобально; npm-пакет имеет SLSA provenance, но это не заменяет проверку кода.

## 9. Воспроизведение

```powershell
# 1. Установка REA CLI (Node.js 22.19+/24.11+/26+)
cmd.exe /d /c "npm install -g rea-agents@6.7.0"

# 2. Копирование цели из WindowsApps в локальный каталог
$src = 'C:\Program Files\WindowsApps\StudioKyome.RunCat_3.6.0.0_x64__nts6hjhfqpj36'
$dst = "$env:LOCALAPPDATA\rea-targets\RunCat365-3.6.0"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item -LiteralPath "$src\RunCat365\RunCat 365.dll" -Destination $dst -Force

# 3. Статический разбор
rea inspect-managed-artifact "$dst\RunCat 365.dll" --json
rea inspect-managed-members "$dst\RunCat 365.dll" --json
rea inspect-managed-native-boundaries "$dst\RunCat 365.dll" --json

# 4. Исходники для сверки
git clone --depth 1 --branch 3.6.0 https://github.com/runcat-dev/RunCat365.git
```

## 10. Ссылки на исходники

- `Program.cs`: https://github.com/runcat-dev/RunCat365/blob/3.6.0/RunCat365/Program.cs
  - константы и таймеры: строки 54-145;
  - `CalculateInterval`: строки 301-312;
  - `FetchSystemInfo`: строки 314-344;
  - `FetchTick`: строки 346-381.
- `CPURepository.cs`: https://github.com/runcat-dev/RunCat365/blob/3.6.0/RunCat365/CPURepository.cs
  - счетчики: строки 48-126;
  - репозиторий и среднее: строки 128-182.
- `ContextMenuManager.cs`: https://github.com/runcat-dev/RunCat365/blob/3.6.0/RunCat365/ContextMenuManager.cs
  - `NotifyIcon` и меню: строки 23-209;
  - `SetIcons`/`ReplaceIconList`: строки 308-374;
  - `AdvanceFrame`: строки 449-458.
- `BitmapExtension.cs`: https://github.com/runcat-dev/RunCat365/blob/3.6.0/RunCat365/BitmapExtension.cs (строки 42-101).
- `FPSMaxLimit.cs`: https://github.com/runcat-dev/RunCat365/blob/3.6.0/RunCat365/FPSMaxLimit.cs (строки 41-51).
- `LaunchAtStartupManager.cs`: https://github.com/runcat-dev/RunCat365/blob/3.6.0/RunCat365/LaunchAtStartupManager.cs (строки 26-136).
- `CustomRunnerRepository.cs`: https://github.com/runcat-dev/RunCat365/blob/3.6.0/RunCat365/CustomRunnerRepository.cs (строки 22-43, 94-121).
- `RunCat365.csproj`: https://github.com/runcat-dev/RunCat365/blob/3.6.0/RunCat365/RunCat365.csproj (net9.0-windows, WinForms, x64).
