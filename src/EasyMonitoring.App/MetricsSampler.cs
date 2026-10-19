using EasyMonitoring.App.Logging;
using EasyMonitoring.App.Native;
using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.App;

internal sealed class MetricsSampler : IDisposable
{
    private const int SlowSampleEvery = 2;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly CpuLoadCalculator _cpuLoadCalculator = new();
    private readonly MetricsAggregator _aggregator = new(sampleWindow: 3);
    private readonly LhmMetricsSource _lhm = new();
    private Task? _samplingTask;
    private LhmReading _cachedLhm = new(null, null, null, null);
    private MemoryStatus? _cachedMemory;
    private int _tick;
    private bool _cpuErrorLogged;
    private bool _ramErrorLogged;
    private bool _disposed;

    public event Action<MetricsSnapshot>? SnapshotReady;

    public void Start()
    {
        if (_samplingTask is not null || _disposed)
        {
            return;
        }

        _samplingTask = Task.Run(() => RunAsync(_cancellation.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        TryReadCpu(); // Prime the GetSystemTimes baseline.

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var snapshot = Sample();

                try
                {
                    SnapshotReady?.Invoke(snapshot);
                }
                catch (Exception exception)
                {
                    AppLog.Error("A metrics subscriber failed.", exception);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppLog.Error("Metrics sampling stopped unexpectedly.", exception);
        }
    }

    private MetricsSnapshot Sample()
    {
        _tick++;
        var cpuLoad = TryReadCpu();

        // CPU load drives animation and is sampled every second.
        // LHM (CPU/GPU temperature, GPU load) and RAM change slowly enough for 2 s.
        if (_tick % SlowSampleEvery == 1)
        {
            _cachedLhm = _lhm.Read();
            _cachedMemory = TryReadMemory();
        }

        var raw = new MetricsSnapshot(
            CpuLoadPercent: cpuLoad,
            CpuTemperatureC: _cachedLhm.CpuTemperatureC,
            GpuLoadPercent: _cachedLhm.GpuLoadPercent,
            GpuTemperatureC: _cachedLhm.GpuTemperatureC,
            RamUsedGb: _cachedMemory?.UsedGb,
            RamTotalGb: _cachedMemory?.TotalGb,
            RamUsedPercent: _cachedMemory?.UsedPercent);

        return _aggregator.Add(raw);
    }

    private double? TryReadCpu()
    {
        try
        {
            return _cpuLoadCalculator.AddSample(SystemTimes.Read());
        }
        catch (Exception exception)
        {
            if (!_cpuErrorLogged)
            {
                _cpuErrorLogged = true;
                AppLog.Error("Failed to read CPU times.", exception);
            }

            return null;
        }
    }

    private MemoryStatus? TryReadMemory()
    {
        try
        {
            return GlobalMemoryStatusEx.Read();
        }
        catch (Exception exception)
        {
            if (!_ramErrorLogged)
            {
                _ramErrorLogged = true;
                AppLog.Error("Failed to read memory status.", exception);
            }

            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();

        try
        {
            _samplingTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _lhm.Dispose();
        _cancellation.Dispose();
    }
}
