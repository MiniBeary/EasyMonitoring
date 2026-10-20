# EasyMonitoring v1.0.1

Maintenance-релиз после pre-release `v1.0.0`: без новых пользовательских функций, с исправлениями архитектуры и поведения. Первый стабильный релиз.

## Что изменилось

- **Metrics:** `MetricsSnapshotModule` владеет cadence 1 s/2 s, свежестью и средними. GPU-среднее больше не учитывает cached-значения повторно; пропуски держат последнее значение один цикл, затем показывают `N/A`.
- **Autostart:** `AutostartStateModule` различает known/unknown/disabled. Неизвестное состояние больше не считается выключенным; пункт остаётся доступным для повторной попытки, а после ошибки toggle перечитывает фактическое состояние задачи.
- **Sensors:** сенсоры привязываются по стабильным `SensorId`, без повторного поиска по имени. Если нет `Core (Tctl/Tdie)`, `CPU Package` или `Core*`/`CCD*`, температура CPU показывается как `N/A`, а не как посторонний сенсор.
- **Tray:** host и presentation разделены; lifecycle корректно останавливает sampler до закрытия LHM. Визуальный формат, меню и ключи `ui-diagnostics.txt` не изменились.
- **Tests/docs:** добавлены App.Tests, `CONTEXT.md`, обновлены AGENTS, README и QA. Всего 64 теста.

## Установка

1. Установите PawnIO — без него температура CPU будет `N/A`:
   ```powershell
   winget install --id namazso.PawnIO -e
   ```
2. Скачайте `EasyMonitoring-win-x64.exe` из этого релиза.
3. Запустите exe и подтвердите UAC.
4. Правый клик по иконке в трее открывает меню; `Exit` завершает приложение.

## Проверка файла

В релизе есть `SHA256SUMS.txt`:

```powershell
(Get-FileHash .\EasyMonitoring-win-x64.exe -Algorithm SHA256).Hash.ToLower()
Get-Content .\SHA256SUMS.txt
```

## Важно

- exe не подписан; SmartScreen может показать предупреждение.
- Нужны права администратора: приложение читает аппаратные сенсоры.
- Набор GPU-сенсоров зависит от драйвера и LibreHardwareMonitor.
- Без PawnIO температура CPU показывается как `N/A`.

## Лицензия

Код — MIT. Сторонние компоненты и их лицензии перечислены в [THIRD-PARTY-NOTICES.md](https://github.com/MiniBeary/EasyMonitoring/blob/main/THIRD-PARTY-NOTICES.md).
