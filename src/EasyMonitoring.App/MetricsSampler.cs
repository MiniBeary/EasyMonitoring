using EasyMonitoring.App.Logging;
using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.App;

internal sealed class MetricsSampler : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly MetricsSource _source = new();
    private readonly MetricsSnapshotModule _module;
    private Task? _samplingTask;
    private bool _disposed;

    public MetricsSampler()
    {
        _module = new MetricsSnapshotModule(_source);
    }

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
        _source.ReadCpuLoad(); // Prime the GetSystemTimes baseline.

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var snapshot = _module.Poll(DateTimeOffset.UtcNow);

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

    public async Task StopAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();

        if (_samplingTask is not null)
        {
            var completed = await Task.WhenAny(
                _samplingTask,
                Task.Delay(TimeSpan.FromSeconds(2)));

            if (completed != _samplingTask)
            {
                AppLog.Error("Metrics sampling did not stop before shutdown timeout.");
                return;
            }

            try
            {
                await _samplingTask;
            }
            catch (Exception exception)
            {
                AppLog.Error("Metrics sampling failed during shutdown.", exception);
            }
        }

        _source.Dispose();
        _cancellation.Dispose();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
