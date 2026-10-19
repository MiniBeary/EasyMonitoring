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
            var cpu = _computer!.Hardware.FirstOrDefault(
                hardware => hardware.HardwareType == HardwareType.Cpu);

            if (cpu is not null)
            {
                Update(cpu);
            }

            var gpuReadings = new List<HardwareReadings>();
            foreach (var hardware in _computer.Hardware.Where(IsGpu))
            {
                Update(hardware);
                gpuReadings.Add(ToReadings(hardware));
            }

            var cpuSensors = cpu is null
                ? Array.Empty<SensorReading>()
                : ToSensors(cpu).ToArray();

            var cpuTemperature = SensorSelector.SelectCpuTemperature(cpuSensors);
            var gpu = SensorSelector.SelectGpu(gpuReadings);

            double? gpuLoad = null;
            double? gpuTemperature = null;

            if (gpu is not null)
            {
                gpuLoad = SanitizePercent(SensorSelector.SelectGpuLoad(gpu)?.Value);
                gpuTemperature = SanitizeTemperature(SensorSelector.SelectGpuTemperature(gpu)?.Value);
            }

            var cpuTemperatureC = SanitizeTemperature(cpuTemperature?.Value);

            if (cpuTemperatureC is null && !_missingCpuTemperatureLogged)
            {
                _missingCpuTemperatureLogged = true;
                AppLog.Warning(
                    "CPU temperature is unavailable. Install PawnIO (winget install namazso.PawnIO) and restart EasyMonitoring.");
            }

            return new LhmReading(cpuTemperatureC, gpuLoad, gpuTemperature, gpu?.Name);
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
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Error("Failed to open LibreHardwareMonitor.", exception);
            _computer = null;
            return false;
        }
    }

    private static bool IsGpu(IHardware hardware)
    {
        return hardware.HardwareType is HardwareType.GpuNvidia
            or HardwareType.GpuAmd
            or HardwareType.GpuIntel;
    }

    private static void Update(IHardware hardware)
    {
        hardware.Update();

        foreach (var subHardware in hardware.SubHardware)
        {
            Update(subHardware);
        }
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
        foreach (var sensor in hardware.Sensors)
        {
            var kind = sensor.SensorType switch
            {
                SensorType.Load => SensorKind.Load,
                SensorType.Temperature => SensorKind.Temperature,
                _ => SensorKind.Other,
            };

            if (kind == SensorKind.Other)
            {
                continue;
            }

            yield return new SensorReading(sensor.Name, kind, sensor.Value);
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            foreach (var sensor in ToSensors(subHardware))
            {
                yield return sensor;
            }
        }
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
