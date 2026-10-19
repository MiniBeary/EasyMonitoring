using EasyMonitoring.App.Logging;
using EasyMonitoring.Core.Sensors;
using LibreHardwareMonitor.Hardware;

namespace EasyMonitoring.App;

internal sealed record LhmReading(
    double? CpuTemperatureC,
    double? GpuLoadPercent,
    double? GpuTemperatureC,
    string? GpuName);

internal sealed class LhmMetricsSource : IDisposable
{
    private Computer? _computer;
    private IHardware? _cpuHardware;
    private ISensor? _cpuTemperatureSensor;
    private IHardware? _gpuHardware;
    private ISensor? _gpuLoadSensor;
    private ISensor? _gpuTemperatureSensor;
    private string? _gpuName;
    private bool _openAttempted;
    private bool _readErrorLogged;
    private bool _missingCpuTemperatureLogged;
    private bool _disposed;

    public LhmReading Read()
    {
        if (_disposed || !TryOpen())
        {
            return new LhmReading(null, null, null, null);
        }

        try
        {
            if (_cpuTemperatureSensor is not null)
            {
                _cpuHardware?.Update();
            }

            if (_gpuLoadSensor is not null || _gpuTemperatureSensor is not null)
            {
                _gpuHardware?.Update();
            }

            return new LhmReading(
                SanitizeTemperature(_cpuTemperatureSensor?.Value),
                SanitizePercent(_gpuLoadSensor?.Value),
                SanitizeTemperature(_gpuTemperatureSensor?.Value),
                _gpuName);
        }
        catch (Exception exception)
        {
            if (!_readErrorLogged)
            {
                _readErrorLogged = true;
                AppLog.Error("Failed to read LibreHardwareMonitor sensors.", exception);
            }

            return new LhmReading(null, null, null, null);
        }
    }

    private bool TryOpen()
    {
        if (_openAttempted)
        {
            return _computer is not null;
        }

        _openAttempted = true;

        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
            };

            _computer.Open();
            ResolveSensors();
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Error("Failed to open LibreHardwareMonitor.", exception);
            _computer = null;
            return false;
        }
    }

    private void ResolveSensors()
    {
        var hardware = _computer!.Hardware;

        _cpuHardware = hardware.FirstOrDefault(item => item.HardwareType == HardwareType.Cpu);

        if (_cpuHardware is not null)
        {
            _cpuHardware.Update();

            var cpuTemperature = SensorSelector.SelectCpuTemperature(
                ToSensors(_cpuHardware).ToArray());

            _cpuTemperatureSensor = FindSensor(_cpuHardware, cpuTemperature);

            if (_cpuTemperatureSensor is null && !_missingCpuTemperatureLogged)
            {
                _missingCpuTemperatureLogged = true;
                AppLog.Warning(
                    "CPU temperature is unavailable. Install PawnIO (winget install namazso.PawnIO) and restart EasyMonitoring.");
            }
        }

        var gpuReadings = new List<HardwareReadings>();
        foreach (var item in hardware.Where(IsGpu))
        {
            item.Update();
            gpuReadings.Add(ToReadings(item));
        }

        var gpu = SensorSelector.SelectGpu(gpuReadings);
        if (gpu is null)
        {
            return;
        }

        _gpuHardware = hardware.FirstOrDefault(
            item => IsGpu(item) && string.Equals(item.Name, gpu.Name, StringComparison.Ordinal));
        _gpuName = gpu.Name;
        _gpuLoadSensor = FindSensor(_gpuHardware, SensorSelector.SelectGpuLoad(gpu));
        _gpuTemperatureSensor = FindSensor(_gpuHardware, SensorSelector.SelectGpuTemperature(gpu));
    }

    private static bool IsGpu(IHardware hardware)
    {
        return hardware.HardwareType is HardwareType.GpuNvidia
            or HardwareType.GpuAmd
            or HardwareType.GpuIntel;
    }

    private static HardwareReadings ToReadings(IHardware hardware)
    {
        return new HardwareReadings(
            hardware.HardwareType.ToString(),
            hardware.Name,
            ToSensors(hardware).ToArray());
    }

    private static IEnumerable<SensorReading> ToSensors(IHardware hardware)
    {
        foreach (var sensor in EnumerateSensors(hardware))
        {
            var kind = ToKind(sensor.SensorType);
            if (kind == SensorKind.Other)
            {
                continue;
            }

            yield return new SensorReading(sensor.Name, kind, sensor.Value);
        }
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            yield return sensor;
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var sensor in EnumerateSensors(subHardware))
            {
                yield return sensor;
            }
        }
    }

    private static ISensor? FindSensor(IHardware? hardware, SensorReading? reading)
    {
        if (hardware is null || reading is null)
        {
            return null;
        }

        return EnumerateSensors(hardware).FirstOrDefault(sensor =>
            string.Equals(sensor.Name, reading.Name, StringComparison.Ordinal) &&
            ToKind(sensor.SensorType) == reading.Kind);
    }

    private static SensorKind ToKind(SensorType sensorType)
    {
        return sensorType switch
        {
            SensorType.Load => SensorKind.Load,
            SensorType.Temperature => SensorKind.Temperature,
            _ => SensorKind.Other,
        };
    }

    private static double? SanitizeTemperature(double? value)
    {
        return value is > 0 and < 150 ? value : null;
    }

    private static double? SanitizePercent(double? value)
    {
        return value is >= 0 and <= 100 ? value : null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cpuHardware = null;
        _cpuTemperatureSensor = null;
        _gpuHardware = null;
        _gpuLoadSensor = null;
        _gpuTemperatureSensor = null;

        try
        {
            _computer?.Close();
        }
        catch (Exception exception)
        {
            AppLog.Error("Failed to close LibreHardwareMonitor.", exception);
        }
        finally
        {
            _computer = null;
        }
    }
}
