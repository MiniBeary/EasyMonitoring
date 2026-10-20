namespace EasyMonitoring.Core.Sensors;

public enum HardwareKind
{
    Cpu,
    GpuNvidia,
    GpuAmd,
    GpuIntel,
    Other,
}

public readonly record struct SensorId(int Value);

public readonly record struct HardwareId(int Value);

public sealed record SensorDescriptor(
    SensorId Id,
    string Name,
    SensorKind Kind);

public sealed record HardwareDescriptor(
    HardwareId Id,
    HardwareKind Kind,
    string Name,
    IReadOnlyList<SensorDescriptor> Sensors);

public sealed record SensorSelection(
    SensorId? CpuTemperature,
    HardwareId? GpuHardware,
    SensorId? GpuLoad,
    SensorId? GpuTemperature);
