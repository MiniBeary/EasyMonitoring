using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.App.Tests;

public class MetricsSamplerTests
{
    [Fact]
    public async Task Stop_before_start_disposes_once_and_prevents_later_start()
    {
        var source = new ControlledSource();
        using var sampler = Create(source);
        await sampler.StopAsync();
        sampler.Start();
        await sampler.StopAsync();
        Assert.Equal(1, source.Disposals);
        Assert.Equal(0, source.Reads);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Timed_out_read_is_not_disposed_underneath_and_cleans_up_when_it_finishes(int blockRead)
    {
        var source = new ControlledSource { BlockOnRead = blockRead };
        using var sampler = Create(source);
        var snapshots = 0;
        sampler.SnapshotReady += _ => Interlocked.Increment(ref snapshots);
        sampler.Start();
        await source.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var stop = sampler.StopAsync();
            Assert.Same(stop, sampler.StopAsync());
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, source.Disposals);
        }
        finally { source.Release.Set(); }
        await source.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sampler.StopAsync();
        Assert.Equal(1, source.Disposals);
        Assert.Equal(0, snapshots);
    }

    [Fact]
    public async Task A_failure_during_baseline_read_also_releases_the_source()
    {
        var source = new ControlledSource { Throw = true };
        using var sampler = Create(source);
        sampler.Start();
        await source.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sampler.StopAsync();
        Assert.Equal(1, source.Disposals);
    }

    [Fact]
    public async Task Normal_sampling_still_delivers_metrics_then_stops_once()
    {
        var source = new ControlledSource();
        using var sampler = Create(source);
        var delivered = new TaskCompletionSource<MetricsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        sampler.SnapshotReady += snapshot => delivered.TrySetResult(snapshot);
        sampler.Start();
        var snapshot = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(25, snapshot.CpuLoadPercent);
        await Task.WhenAll(sampler.StopAsync(), sampler.StopAsync());
        Assert.Equal(1, source.Disposals);
    }

    private static MetricsSampler Create(ControlledSource source) => new(source,
        samplingInterval: TimeSpan.FromMilliseconds(10), shutdownTimeout: TimeSpan.FromMilliseconds(50),
        logError: (_, _) => { });

    private sealed class ControlledSource : IDisposableMetricsSource
    {
        public int BlockOnRead { get; init; }
        public bool Throw { get; init; }
        public int Reads;
        public int Disposals;
        public ManualResetEventSlim Release { get; } = new(false);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public double? ReadCpuLoad()
        {
            var read = Interlocked.Increment(ref Reads);
            Entered.TrySetResult();
            if (read == BlockOnRead)
            {
                Blocked.TrySetResult();
                if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test gate was not released.");
            }
            if (Throw) throw new InvalidOperationException("test read failure");
            return 25;
        }
        public HardwareSample ReadHardware() => new(50, 10, 40);
        public MemorySample? ReadMemory() => new(8, 32, 25);
        public void Dispose()
        {
            Interlocked.Increment(ref Disposals);
            Disposed.TrySetResult();
        }
    }
}
