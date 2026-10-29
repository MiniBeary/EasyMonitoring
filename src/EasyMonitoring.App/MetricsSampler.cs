using EasyMonitoring.App.Logging;
using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.App;

internal sealed class MetricsSampler : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly IDisposableMetricsSource _source;
    private readonly MetricsSnapshotModule _module;
    private readonly TimeSpan _samplingInterval;
    private readonly TimeSpan _shutdownTimeout;
    private readonly Action<string, Exception?> _logError;
    private Task? _samplingTask;
    private Task? _stopTask;
    private bool _stopRequested;
    private int _cleanupStarted;

    public MetricsSampler() : this(new MetricsSource()) { }

    internal MetricsSampler(IDisposableMetricsSource source, TimeSpan? samplingInterval = null,
        TimeSpan? shutdownTimeout = null, Action<string, Exception?>? logError = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _module = new MetricsSnapshotModule(source);
        _samplingInterval = samplingInterval ?? TimeSpan.FromSeconds(1);
        _shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(2);
        if (_samplingInterval <= TimeSpan.Zero || _shutdownTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(samplingInterval), "Intervals must be positive.");
        _logError = logError ?? AppLog.Error;
    }

    public event Action<MetricsSnapshot>? SnapshotReady;

    public void Start()
    {
        lock (_gate)
        {
            if (_samplingTask is not null || _stopRequested) return;
            var token = _cancellation.Token;
            _samplingTask = Task.Run(() => RunAsync(token));
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            _source.ReadCpuLoad();
            using var timer = new PeriodicTimer(_samplingInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var snapshot = _module.Poll(DateTimeOffset.UtcNow);
                if (cancellationToken.IsCancellationRequested) break;
                try { SnapshotReady?.Invoke(snapshot); }
                catch (Exception exception) { _logError("A metrics subscriber failed.", exception); }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { _logError("Metrics sampling stopped unexpectedly.", exception); }
        finally { Cleanup(); }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (_stopTask is not null) return _stopTask;
            _stopRequested = true;
            if (_cleanupStarted == 0) _cancellation.Cancel();
            if (_samplingTask is null)
            {
                Cleanup();
                return _stopTask = Task.CompletedTask;
            }
            return _stopTask = WaitForStopAsync(_samplingTask);
        }
    }

    private async Task WaitForStopAsync(Task samplingTask)
    {
        try { await samplingTask.WaitAsync(_shutdownTimeout).ConfigureAwait(false); }
        catch (TimeoutException) { _logError("Metrics sampling did not stop before shutdown timeout; cleanup will follow read completion.", null); }
        catch (Exception exception) { _logError("Metrics sampling failed during shutdown.", exception); }
    }

    private void Cleanup()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0) return;
        try { _source.Dispose(); }
        catch (Exception exception) { _logError("Failed to release metrics source.", exception); }
        finally { lock (_gate) _cancellation.Dispose(); }
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
