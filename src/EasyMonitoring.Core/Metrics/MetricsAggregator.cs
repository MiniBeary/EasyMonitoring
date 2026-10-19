namespace EasyMonitoring.Core.Metrics;

public sealed class MetricsAggregator
{
    private readonly MovingAverage _cpuLoad;
    private readonly MovingAverage _gpuLoad;

    public MetricsAggregator(int sampleWindow = 3)
    {
        _cpuLoad = new MovingAverage(sampleWindow);
        _gpuLoad = new MovingAverage(sampleWindow);
    }

    public MetricsSnapshot Add(MetricsSnapshot raw)
    {
        var cpuLoad = _cpuLoad.Add(raw.CpuLoadPercent);
        var gpuLoad = raw.GpuLoadPercent is null ? null : _gpuLoad.Add(raw.GpuLoadPercent);

        return raw with
        {
            CpuLoadPercent = cpuLoad,
            GpuLoadPercent = gpuLoad,
        };
    }
}
