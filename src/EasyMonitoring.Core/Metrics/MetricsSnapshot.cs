namespace EasyMonitoring.Core.Metrics;

public sealed record MetricsSnapshot(
    double? CpuLoadPercent,
    double? CpuTemperatureC,
    double? GpuLoadPercent,
    double? GpuTemperatureC,
    double? RamUsedGb,
    double? RamTotalGb,
    double? RamUsedPercent)
{
    public static MetricsSnapshot Empty { get; } = new(null, null, null, null, null, null, null);
}
