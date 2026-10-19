using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.Core.Tests;

public class MetricsAggregatorTests
{
    [Fact]
    public void Add_averages_cpu_load_over_three_samples()
    {
        var aggregator = new MetricsAggregator(3);
        aggregator.Add(Snapshot(cpuLoad: 10));
        aggregator.Add(Snapshot(cpuLoad: 20));

        var result = aggregator.Add(Snapshot(cpuLoad: 30));

        Assert.Equal(20, result.CpuLoadPercent);
    }

    [Fact]
    public void Add_evicts_old_cpu_samples_when_the_window_is_full()
    {
        var aggregator = new MetricsAggregator(3);
        aggregator.Add(Snapshot(cpuLoad: 10));
        aggregator.Add(Snapshot(cpuLoad: 20));
        aggregator.Add(Snapshot(cpuLoad: 30));

        var result = aggregator.Add(Snapshot(cpuLoad: 40));

        Assert.Equal(30, result.CpuLoadPercent);
    }

    [Fact]
    public void Add_averages_gpu_load_over_three_samples()
    {
        var aggregator = new MetricsAggregator(3);
        aggregator.Add(Snapshot(gpuLoad: 10));
        aggregator.Add(Snapshot(gpuLoad: 20));

        var result = aggregator.Add(Snapshot(gpuLoad: 30));

        Assert.Equal(20, result.GpuLoadPercent);
    }

    [Fact]
    public void Add_keeps_the_latest_temperatures_and_ram_values()
    {
        var aggregator = new MetricsAggregator(3);

        var result = aggregator.Add(new MetricsSnapshot(
            CpuLoadPercent: 10,
            CpuTemperatureC: 58,
            GpuLoadPercent: 20,
            GpuTemperatureC: 47,
            RamUsedGb: 8.2,
            RamTotalGb: 31.9,
            RamUsedPercent: 26));

        Assert.Equal(58, result.CpuTemperatureC);
        Assert.Equal(47, result.GpuTemperatureC);
        Assert.Equal(8.2, result.RamUsedGb);
        Assert.Equal(31.9, result.RamTotalGb);
        Assert.Equal(26, result.RamUsedPercent);
    }

    [Fact]
    public void Add_returns_null_gpu_load_when_the_current_sample_has_no_gpu()
    {
        var aggregator = new MetricsAggregator(3);
        aggregator.Add(Snapshot(cpuLoad: 10, gpuLoad: 20));

        var result = aggregator.Add(Snapshot(cpuLoad: 30, gpuLoad: null));

        Assert.Null(result.GpuLoadPercent);
        Assert.Equal(20, result.CpuLoadPercent);
    }

    [Fact]
    public void Add_keeps_the_previous_cpu_average_when_the_current_sample_is_missing()
    {
        var aggregator = new MetricsAggregator(3);
        aggregator.Add(Snapshot(cpuLoad: 20));

        var result = aggregator.Add(Snapshot(cpuLoad: null));

        Assert.Equal(20, result.CpuLoadPercent);
    }

    private static MetricsSnapshot Snapshot(double? cpuLoad = null, double? gpuLoad = null)
    {
        return new MetricsSnapshot(
            CpuLoadPercent: cpuLoad,
            CpuTemperatureC: null,
            GpuLoadPercent: gpuLoad,
            GpuTemperatureC: null,
            RamUsedGb: null,
            RamTotalGb: null,
            RamUsedPercent: null);
    }
}
