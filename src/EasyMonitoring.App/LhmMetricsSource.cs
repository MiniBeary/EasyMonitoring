using EasyMonitoring.App.Logging;
using EasyMonitoring.Core.Sensors;
using LibreHardwareMonitor.Hardware;

namespace EasyMonitoring.App;

internal sealed record LhmReading(
    double? CpuTemperatureC,
    double? GpuLoadPercent,
    double? GpuTemperatureC);

internal sealed class LhmMetricsSource : IDisposable
{
    private readonly Dictionary<SensorId, ISensor> _sensorById = new();
    private readonly Dictionary<HardwareId, IHardware> _hardwareById = new();

    private Computer? _computer;
    private IHardware? _cpuHardware;
    private ISensor? _cpuTemperatureSensor;
    private IHardware? _gpuHardware;
    private ISensor? _gpuLoadSensor;
    private ISensor? _gpuTemperatureSensor;
    private int _nextSensorId;
    private int _nextHardwareId;
    private bool _openAttempted;
    private bool _readErrorLogged;
    private bool _missingCpuTemperatureLogged;
    private bool _disposed;

    public LhmReading Read()
    {
        if (_disposed || !TryOpen())
        {
            return new LhmReading(null, null, null);
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
                SensorValuePolicy.Temperature(_cpuTemperatureSensor?.Value),
                SensorValuePolicy.Percent(_gpuLoadSensor?.Value),
                SensorValuePolicy.Temperature(_gpuTemperatureSensor?.Value));
        }
        catch (Exception exception)
        {
            if (!_readErrorLogged)
            {
                _readErrorLogged = true;
                AppLog.Error("Failed to read LibreHardwareMonitor sensors.", exception);
            }

            return new LhmReading(null, null, null);
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

        _sensorById.Clear();
        _hardwareById.Clear();
        _nextSensorId = 0;
        _nextHardwareId = 0;
        _cpuTemperatureSensor = null;
        _gpuHardware = null;
        _gpuLoadSensor = null;
        _gpuTemperatureSensor = null;

        _cpuHardware = hardware.FirstOrDefault(item => item.HardwareType == HardwareType.Cpu);

        HardwareDescriptor? cpuDescriptor = null;
        if (_cpuHardware is not null)
        {
            _cpuHardware.Update();
            cpuDescriptor = ToDescriptor(_cpuHardware, HardwareKind.Cpu);
        }

        var gpuDescriptors = new List<HardwareDescriptor>();
        foreach (var item in hardware.Where(IsGpu))
        {
            item.Update();
            gpuDescriptors.Add(ToDescriptor(item, ToGpuKind(item.HardwareType)));
        }

        var selection = SensorResolver.Resolve(cpuDescriptor, gpuDescriptors);

        if (selection.CpuTemperature is { } cpuTemperatureId)
        {
            _cpuTemperatureSensor = _sensorById[cpuTemperatureId];
        }

        if (selection.GpuHardware is { } gpuHardwareId)
        {
            _gpuHardware = _hardwareById[gpuHardwareId];
        }

        if (selection.GpuLoad is { } gpuLoadId)
        {
            _gpuLoadSensor = _sensorById[gpuLoadId];
        }

        if (selection.GpuTemperature is { } gpuTemperatureId)
        {
            _gpuTemperatureSensor = _sensorById[gpuTemperatureId];
        }

        if (_cpuTemperatureSensor is null && !_missingCpuTemperatureLogged)
        {
            _missingCpuTemperatureLogged = true;
            AppLog.Warning(
                "CPU temperature is unavailable. Install PawnIO (winget install namazso.PawnIO) and restart EasyMonitoring.");
        }
    }

    private HardwareDescriptor ToDescriptor(IHardware hardware, HardwareKind kind)
    {
        var hardwareId = new HardwareId(++_nextHardwareId);
        _hardwareById[hardwareId] = hardware;

        var sensors = new List<SensorDescriptor>();
        foreach (var sensor in EnumerateSensors(hardware))
        {
            var sensorKind = ToKind(sensor.SensorType);
            if (sensorKind == SensorKind.Other)
            {
                continue;
            }

            var sensorId = new SensorId(++_nextSensorId);
            _sensorById[sensorId] = sensor;
            sensors.Add(new SensorDescriptor(sensorId, sensor.Name, sensorKind));
        }

        return new HardwareDescriptor(hardwareId, kind, hardware.Name, sensors);
    }

    private static bool IsGpu(IHardware hardware)
    {
        return hardware.HardwareType is HardwareType.GpuNvidia
            or HardwareType.GpuAmd
            or HardwareType.GpuIntel;
    }

    private static HardwareKind ToGpuKind(HardwareType hardwareType)
    {
        return hardwareType switch
        {
            HardwareType.GpuNvidia => HardwareKind.GpuNvidia,
            HardwareType.GpuAmd => HardwareKind.GpuAmd,
            HardwareType.GpuIntel => HardwareKind.GpuIntel,
            _ => HardwareKind.Other,
        };
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

    private static SensorKind ToKind(SensorType sensorType)
    {
        return sensorType switch
        {
            SensorType.Load => SensorKind.Load,
            SensorType.Temperature => SensorKind.Temperature,
            _ => SensorKind.Other,
        };
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
        _sensorById.Clear();
        _hardwareById.Clear();

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
