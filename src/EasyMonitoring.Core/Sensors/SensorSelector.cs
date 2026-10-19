namespace EasyMonitoring.Core.Sensors;

public static class SensorSelector
{
    private static readonly string[] CpuTemperaturePriority =
    [
        "Core (Tctl/Tdie)",
        "CPU Package",
        "Core Average",
        "Core Max",
        "Tdie",
        "Package",
    ];

    private static readonly string[] GpuTypePriority =
    [
        "GpuNvidia",
        "GpuAmd",
        "GpuIntel",
    ];

    public static SensorReading? SelectCpuTemperature(IEnumerable<SensorReading> sensors)
    {
        var temperatures = sensors
            .Where(sensor => sensor.Kind == SensorKind.Temperature)
            .ToList();

        foreach (var name in CpuTemperaturePriority)
        {
            var match = temperatures.FirstOrDefault(
                sensor => string.Equals(sensor.Name, name, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        var thermal = temperatures.FirstOrDefault(sensor => ContainsAny(sensor.Name, "Tctl", "Tdie"));
        if (thermal is not null)
        {
            return thermal;
        }

        var core = temperatures.FirstOrDefault(sensor => ContainsAny(sensor.Name, "Core", "CCD"));
        if (core is not null)
        {
            return core;
        }

        return temperatures.FirstOrDefault();
    }

    public static HardwareReadings? SelectGpu(IEnumerable<HardwareReadings> hardware)
    {
        var gpus = hardware
            .Where(item => IsGpuType(item.HardwareType))
            .ToList();

        foreach (var type in GpuTypePriority)
        {
            var match = gpus.FirstOrDefault(
                item => string.Equals(item.HardwareType, type, StringComparison.OrdinalIgnoreCase) &&
                        HasGpuSensor(item));

            if (match is not null)
            {
                return match;
            }
        }

        return gpus.FirstOrDefault(HasGpuSensor);
    }

    public static SensorReading? SelectGpuLoad(HardwareReadings gpu)
    {
        var loads = gpu.Sensors
            .Where(sensor => sensor.Kind == SensorKind.Load)
            .ToList();

        return loads.FirstOrDefault(
                   sensor => string.Equals(sensor.Name, "GPU Core", StringComparison.OrdinalIgnoreCase))
               ?? loads.FirstOrDefault(sensor => ContainsAny(sensor.Name, "GPU Core"))
               ?? loads.FirstOrDefault();
    }

    public static SensorReading? SelectGpuTemperature(HardwareReadings gpu)
    {
        var temperatures = gpu.Sensors
            .Where(sensor => sensor.Kind == SensorKind.Temperature)
            .ToList();

        return temperatures.FirstOrDefault(
                   sensor => string.Equals(sensor.Name, "GPU Core", StringComparison.OrdinalIgnoreCase))
               ?? temperatures.FirstOrDefault(sensor => ContainsAny(sensor.Name, "GPU Core"))
               ?? temperatures.FirstOrDefault();
    }

    private static bool HasGpuSensor(HardwareReadings gpu)
    {
        return SelectGpuLoad(gpu) is not null || SelectGpuTemperature(gpu) is not null;
    }

    private static bool IsGpuType(string hardwareType)
    {
        return GpuTypePriority.Contains(hardwareType, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
