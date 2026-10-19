using EasyMonitoring.App.Logging;
using EasyMonitoring.App.Native;
using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.App;

internal sealed class MetricsSampler : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CpuLoadCalculator _cpuLoadCalculator = new();
    private readonly MetricsAggregator _aggregator = new(sampleWindow: 3);
    private readonly LhmMetricsSource _lhm = new();
    private Task? _samplingTask;
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
        var cpuLoad = TryReadCpu();
        var lhm = _lhm.Read();
        var memory = TryReadMemory();

        var raw = new MetricsSnapshot(
            CpuLoadPercent: cpuLoad,
            CpuTemperatureC: lhm.CpuTemperatureC,
            GpuLoadPercent: lhm.GpuLoadPercent,
            GpuTemperatureC: lhm.GpuTemperatureC,
            RamUsedGb: memory?.UsedGb,
            RamTotalGb: memory?.TotalGb,
            RamUsedPercent: memory?.UsedPercent);

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
