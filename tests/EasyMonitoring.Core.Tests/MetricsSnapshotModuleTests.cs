using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.Core.Tests;

public class MetricsSnapshotModuleTests
{
    [Fact]
    public void Poll_reads_cpu_every_tick_and_slow_sources_every_two_seconds()
    {
        var source = new FakeMetricsSource();
        var module = new MetricsSnapshotModule(source);
        var start = DateTimeOffset.UtcNow;

        module.Poll(start);
        module.Poll(start.AddSeconds(1));
        module.Poll(start.AddSeconds(2));

        Assert.Equal(3, source.CpuCalls);
        Assert.Equal(2, source.HardwareCalls);
        Assert.Equal(2, source.MemoryCalls);
    }

    [Fact]
    public void Gpu_average_uses_only_fresh_slow_samples()
    {
        var source = new FakeMetricsSource
        {
            HardwareReader = call => call switch
            {
                0 => new HardwareSample(null, 10, null),
                1 => new HardwareSample(null, 20, null),
                2 => new HardwareSample(null, 30, null),
                _ => new HardwareSample(null, null, null),
            },
        };
        var module = new MetricsSnapshotModule(source);
        var start = DateTimeOffset.UtcNow;

        var snapshots = Enumerable.Range(0, 5)
            .Select(offset => module.Poll(start.AddSeconds(offset)))
            .ToArray();

        Assert.Equal(10d, snapshots[0].GpuLoadPercent!.Value, 3);
        Assert.Equal(10d, snapshots[1].GpuLoadPercent!.Value, 3);
        Assert.Equal(15d, snapshots[2].GpuLoadPercent!.Value, 3);
        Assert.Equal(15d, snapshots[3].GpuLoadPercent!.Value, 3);
        Assert.Equal(20d, snapshots[4].GpuLoadPercent!.Value, 3);
    }

    [Fact]
    public void Cpu_holds_one_poll_then_returns_na_and_recovers()
    {
        var source = new FakeMetricsSource
        {
            CpuReader = call => call switch
            {
                0 => 20,
                1 => null,
                2 => null,
                3 => 40,
                _ => null,
            },
        };
        var module = new MetricsSnapshotModule(source);
        var start = DateTimeOffset.UtcNow;

        Assert.Equal(20d, module.Poll(start).CpuLoadPercent!.Value, 3);
        Assert.Equal(20d, module.Poll(start.AddSeconds(1)).CpuLoadPercent!.Value, 3);
        Assert.Null(module.Poll(start.AddSeconds(2)).CpuLoadPercent);
        Assert.Equal(40d, module.Poll(start.AddSeconds(3)).CpuLoadPercent!.Value, 3);
    }

    [Fact]
    public void Gpu_holds_one_slow_cycle_then_returns_na_and_recovers()
    {
        var source = new FakeMetricsSource
        {
            HardwareReader = call => call switch
            {
                0 => new HardwareSample(null, 10, null),
                1 => new HardwareSample(null, null, null),
                2 => new HardwareSample(null, null, null),
                3 => new HardwareSample(null, 20, null),
                _ => new HardwareSample(null, null, null),
            },
        };
        var module = new MetricsSnapshotModule(source);
        var start = DateTimeOffset.UtcNow;

        Assert.Equal(10d, module.Poll(start).GpuLoadPercent!.Value, 3);
        Assert.Equal(10d, module.Poll(start.AddSeconds(2)).GpuLoadPercent!.Value, 3);
        Assert.Null(module.Poll(start.AddSeconds(4)).GpuLoadPercent);
        Assert.Equal(20d, module.Poll(start.AddSeconds(6)).GpuLoadPercent!.Value, 3);
    }

    [Fact]
    public void Temperature_and_memory_hold_one_slow_cycle_then_return_na()
    {
        var source = new FakeMetricsSource
        {
            HardwareReader = call => call switch
            {
                0 => new HardwareSample(50, null, 60),
                1 => new HardwareSample(null, null, null),
                2 => new HardwareSample(null, null, null),
                _ => new HardwareSample(null, null, null),
            },
            MemoryReader = call => call switch
            {
                0 => new MemorySample(8, 32, 25),
                1 => null,
                2 => null,
                _ => null,
            },
        };
        var module = new MetricsSnapshotModule(source);
        var start = DateTimeOffset.UtcNow;

        var first = module.Poll(start);
        Assert.Equal(50d, first.CpuTemperatureC!.Value, 3);
        Assert.Equal(60d, first.GpuTemperatureC!.Value, 3);
        Assert.Equal(8d, first.RamUsedGb!.Value, 3);

        var second = module.Poll(start.AddSeconds(2));
        Assert.Equal(50d, second.CpuTemperatureC!.Value, 3);
        Assert.Equal(60d, second.GpuTemperatureC!.Value, 3);
        Assert.Equal(8d, second.RamUsedGb!.Value, 3);

        var third = module.Poll(start.AddSeconds(4));
        Assert.Null(third.CpuTemperatureC);
        Assert.Null(third.GpuTemperatureC);
        Assert.Null(third.RamUsedGb);
    }

    [Fact]
    public void Cpu_average_evicts_samples_outside_the_window()
    {
        var source = new FakeMetricsSource
        {
            CpuReader = call => call switch
            {
                0 => 10,
                1 => 20,
                2 => 30,
                3 => 40,
                _ => null,
            },
        };
        var module = new MetricsSnapshotModule(source);
        var start = DateTimeOffset.UtcNow;

        Assert.Equal(10d, module.Poll(start).CpuLoadPercent!.Value, 3);
        Assert.Equal(15d, module.Poll(start.AddSeconds(1)).CpuLoadPercent!.Value, 3);
        Assert.Equal(20d, module.Poll(start.AddSeconds(2)).CpuLoadPercent!.Value, 3);
        Assert.Equal(30d, module.Poll(start.AddSeconds(3)).CpuLoadPercent!.Value, 3);
    }

    [Fact]
    public void Source_exceptions_are_treated_as_missing_values()
    {
        var source = new FakeMetricsSource
        {
            CpuReader = _ => throw new InvalidOperationException("cpu"),
            HardwareReader = _ => throw new InvalidOperationException("hardware"),
            MemoryReader = _ => throw new InvalidOperationException("memory"),
        };
        var module = new MetricsSnapshotModule(source);

        var snapshot = module.Poll(DateTimeOffset.UtcNow);

        Assert.Null(snapshot.CpuLoadPercent);
        Assert.Null(snapshot.CpuTemperatureC);
        Assert.Null(snapshot.GpuLoadPercent);
        Assert.Null(snapshot.RamUsedGb);
    }

    private sealed class FakeMetricsSource : IMetricsSource
    {
        public Func<int, double?> CpuReader { get; set; } = _ => 10;

        public Func<int, HardwareSample> HardwareReader { get; set; } =
            _ => new HardwareSample(45, 5, 40);

        public Func<int, MemorySample?> MemoryReader { get; set; } =
            _ => new MemorySample(8, 32, 25);

        public int CpuCalls { get; private set; }

        public int HardwareCalls { get; private set; }

        public int MemoryCalls { get; private set; }

        public double? ReadCpuLoad() => CpuReader(CpuCalls++);

        public HardwareSample ReadHardware() => HardwareReader(HardwareCalls++);

        public MemorySample? ReadMemory() => MemoryReader(MemoryCalls++);
    }
}
