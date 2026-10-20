namespace EasyMonitoring.Core.Metrics;

public interface IMetricsSource
{
    double? ReadCpuLoad();

    HardwareSample ReadHardware();

    MemorySample? ReadMemory();
}

public readonly record struct HardwareSample(
    double? CpuTemperatureC,
    double? GpuLoadPercent,
    double? GpuTemperatureC);

public readonly record struct MemorySample(
    double UsedGb,
    double TotalGb,
    double UsedPercent);
