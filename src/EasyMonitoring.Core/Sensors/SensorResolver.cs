namespace EasyMonitoring.Core.Sensors;

public static class SensorResolver
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

    private static readonly HardwareKind[] GpuKindPriority =
    [
        HardwareKind.GpuNvidia,
        HardwareKind.GpuAmd,
        HardwareKind.GpuIntel,
    ];

    public static SensorSelection Resolve(
        HardwareDescriptor? cpu,
        IEnumerable<HardwareDescriptor> hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);

        var gpus = hardware
            .Where(item => GpuKindPriority.Contains(item.Kind))
            .ToList();

        var selectedGpu = SelectGpu(gpus);

        return new SensorSelection(
            CpuTemperature: cpu is null ? null : SelectCpuTemperature(cpu),
            GpuHardware: selectedGpu?.Id,
            GpuLoad: selectedGpu is null ? null : SelectGpuLoad(selectedGpu),
            GpuTemperature: selectedGpu is null ? null : SelectGpuTemperature(selectedGpu));
    }

    public static SensorId? SelectCpuTemperature(HardwareDescriptor cpu)
    {
        ArgumentNullException.ThrowIfNull(cpu);

        var temperatures = cpu.Sensors
            .Where(sensor => sensor.Kind == SensorKind.Temperature)
            .ToList();

        foreach (var name in CpuTemperaturePriority)
        {
            var match = temperatures.FirstOrDefault(
                sensor => string.Equals(sensor.Name, name, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match.Id;
            }
        }

        var thermal = temperatures.FirstOrDefault(sensor => ContainsAny(sensor.Name, "Tctl", "Tdie"));
        if (thermal is not null)
        {
            return thermal.Id;
        }

        var core = temperatures.FirstOrDefault(sensor => ContainsAny(sensor.Name, "Core", "CCD"));
        return core?.Id;
    }

    public static HardwareDescriptor? SelectGpu(IEnumerable<HardwareDescriptor> hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);

        var gpus = hardware
            .Where(item => GpuKindPriority.Contains(item.Kind))
            .ToList();

        foreach (var kind in GpuKindPriority)
        {
            var match = gpus.FirstOrDefault(item => item.Kind == kind && HasGpuSensor(item));

            if (match is not null)
            {
                return match;
            }
        }

        return gpus.FirstOrDefault(HasGpuSensor);
    }

    public static SensorId? SelectGpuLoad(HardwareDescriptor gpu)
    {
        ArgumentNullException.ThrowIfNull(gpu);

        var loads = gpu.Sensors
            .Where(sensor => sensor.Kind == SensorKind.Load)
            .ToList();

        return (loads.FirstOrDefault(
                   sensor => string.Equals(sensor.Name, "GPU Core", StringComparison.OrdinalIgnoreCase))
               ?? loads.FirstOrDefault(sensor => ContainsAny(sensor.Name, "GPU Core"))
               ?? loads.FirstOrDefault())?.Id;
    }

    public static SensorId? SelectGpuTemperature(HardwareDescriptor gpu)
    {
        ArgumentNullException.ThrowIfNull(gpu);

        var temperatures = gpu.Sensors
            .Where(sensor => sensor.Kind == SensorKind.Temperature)
            .ToList();

        return (temperatures.FirstOrDefault(
                   sensor => string.Equals(sensor.Name, "GPU Core", StringComparison.OrdinalIgnoreCase))
               ?? temperatures.FirstOrDefault(sensor => ContainsAny(sensor.Name, "GPU Core"))
               ?? temperatures.FirstOrDefault())?.Id;
    }

    private static bool HasGpuSensor(HardwareDescriptor gpu)
    {
        return SelectGpuLoad(gpu) is not null || SelectGpuTemperature(gpu) is not null;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
