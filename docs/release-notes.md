# EasyMonitoring v1.0.0

Первый публичный pre-release лёгкого tray-монитора для Windows.

## Что внутри

- Анимированная 5-кадровая дуга в системном трее; скорость зависит от загрузки CPU.
- CPU/GPU/RAM в подсказке и контекстном меню.
- Тёмное меню с белым текстом и белой галочкой.
- Автозапуск `Start with Windows` через Task Scheduler: elevated-запуск при входе без отдельного UAC-запроса.
- Self-contained single-file exe для Windows x64; .NET Runtime устанавливать не нужно.
- CPU load через `GetSystemTimes`, RAM через `GlobalMemoryStatusEx`, температуры и GPU через LibreHardwareMonitor 0.9.6.

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

- Это **pre-release**: ручная проверка на разных GPU/драйверах ещё продолжается.
- exe не подписан; SmartScreen может показать предупреждение.
- Нужны права администратора: приложение читает аппаратные сенсоры.
- Набор GPU-сенсоров зависит от драйвера и LibreHardwareMonitor.
- Без PawnIO температура CPU показывается как `N/A`.

## Лицензия

Код — MIT. Сторонние компоненты и их лицензии перечислены в [THIRD-PARTY-NOTICES.md](https://github.com/MiniBeary/EasyMonitoring/blob/main/THIRD-PARTY-NOTICES.md).
