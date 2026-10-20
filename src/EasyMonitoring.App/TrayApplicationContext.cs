using EasyMonitoring.App.Autostart;
using EasyMonitoring.App.Logging;
using EasyMonitoring.App.Tray;
using EasyMonitoring.Core.Autostart;
using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SynchronizationContext _uiContext;
    private readonly TrayPresentation _presentation;
    private readonly AutostartStateModule _autostart;
    private readonly MetricsSampler _sampler;
    private bool _autostartBusy;
    private bool _disposed;
    private bool _exiting;

    internal TrayApplicationContext(bool showTrayIcon = true)
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _presentation = new TrayPresentation(showTrayIcon);
        _presentation.AutostartToggleRequested += (_, _) => ToggleAutostart();
        _presentation.ExitRequested += (_, _) => ExitApplication();
        _presentation.MenuOpening += (_, _) => RefreshAutostartStateIfStale();

        _autostart = new AutostartStateModule(new ScheduledTaskAutostartManager());

        _sampler = new MetricsSampler();
        _sampler.SnapshotReady += OnSnapshotReady;
        _sampler.Start();

        _ = Task.Run(RefreshAutostartState);
    }

    internal bool IsDiagnosticsReady
    {
        get
        {
            var diagnostics = _presentation.GetDiagnostics();
            return diagnostics.Snapshots > 0 && diagnostics.AutostartStateLoaded;
        }
    }

    internal TrayDiagnostics GetDiagnostics() => _presentation.GetDiagnostics();

    internal void ExitForDiagnostics() => ExitApplication();

    private void RefreshAutostartStateIfStale()
    {
        if (_autostartBusy || _disposed || _exiting)
        {
            return;
        }

        _ = Task.Run(RefreshAutostartState);
    }

    private void RefreshAutostartState()
    {
        if (_disposed || _exiting)
        {
            return;
        }

        var state = _autostart.Refresh(DateTimeOffset.UtcNow);

        _uiContext.Post(_ =>
        {
            if (_disposed || _exiting)
            {
                return;
            }

            _presentation.ApplyAutostart(state, _presentation.AutostartChecked);
        }, null);
    }

    private void ToggleAutostart()
    {
        if (_autostartBusy || _disposed || _exiting)
        {
            return;
        }

        _autostartBusy = true;
        _presentation.SetAutostartEnabled(false);

        var previous = _presentation.AutostartChecked;
        var desired = !previous;

        _ = Task.Run(() =>
        {
            var result = _autostart.SetEnabled(desired, DateTimeOffset.UtcNow);
            var state = _autostart.State;

            _uiContext.Post(_ =>
            {
                _autostartBusy = false;

                if (_disposed || _exiting)
                {
                    return;
                }

                _presentation.SetAutostartEnabled(true);
                _presentation.ApplyAutostart(state, result.Success ? desired : previous);

                if (!result.Success)
                {
                    _presentation.ShowAutostartError(result.Error);
                }
            }, null);
        });
    }

    private void OnSnapshotReady(MetricsSnapshot snapshot)
    {
        if (_disposed || _exiting)
        {
            return;
        }

        _uiContext.Post(_ => _presentation.ApplySnapshot(snapshot), null);
    }

    private async void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _presentation.SetAutostartEnabled(false);

        try
        {
            await _sampler.StopAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("Failed to stop metrics sampling.", exception);
        }

        DisposePresentation();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _sampler.StopAsync().GetAwaiter().GetResult();
            DisposePresentation();
        }

        base.Dispose(disposing);
    }

    private void DisposePresentation()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sampler.SnapshotReady -= OnSnapshotReady;
        _presentation.Dispose();
    }
}
