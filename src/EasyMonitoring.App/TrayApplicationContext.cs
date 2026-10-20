using EasyMonitoring.App.Autostart;
using EasyMonitoring.App.Icons;
using EasyMonitoring.App.Logging;
using EasyMonitoring.App.UI;
using EasyMonitoring.Core.Animation;
using EasyMonitoring.Core.Autostart;
using EasyMonitoring.Core.Formatting;
using EasyMonitoring.Core.Metrics;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace EasyMonitoring.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SynchronizationContext _uiContext;
    private readonly IReadOnlyList<Icon> _frames;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _cpuItem;
    private readonly ToolStripMenuItem _gpuItem;
    private readonly ToolStripMenuItem _ramItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly AutostartStateModule _autostart;
    private readonly WinFormsTimer _animationTimer;
    private readonly MetricsSampler _sampler;
    private int _frameIndex;
    private bool _autostartBusy;
    private bool _disposed;
    private bool _exiting;

    internal int AppliedSnapshotCount { get; private set; }

    internal bool AutostartChecked => _autostartItem.Checked;

    internal bool AutostartStateLoaded { get; private set; }

    internal Color MenuBackColor => _menu.BackColor;

    internal Color MenuForeColor => _menu.ForeColor;

    internal bool ShowCheckMargin => _menu.ShowCheckMargin;

    internal string TrayText => _notifyIcon.Text;

    internal IReadOnlyList<string> MenuTexts => _menu.Items
        .OfType<ToolStripMenuItem>()
        .Select(item => item.Text ?? string.Empty)
        .ToArray();

    internal int AnimationIntervalMs => _animationTimer.Interval;

    internal int AnimationFrameCount => _frames.Count;

    internal int CurrentFrameIndex => _frameIndex;

    internal void ExitForDiagnostics()
    {
        _exitItem.PerformClick();
    }

    internal TrayApplicationContext(bool showTrayIcon = true)
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _frames = ArcIconFactory.CreateFrames();

        _cpuItem = CreateInfoItem(StatusFormatter.FormatCpu(null, null));
        _gpuItem = CreateInfoItem(StatusFormatter.FormatGpu(null, null));
        _ramItem = CreateInfoItem(StatusFormatter.FormatRam(null, null, null));

        _autostart = new AutostartStateModule(new ScheduledTaskAutostartManager());
        _autostartItem = new ToolStripMenuItem("Start with Windows")
        {
            Enabled = false,
            ForeColor = MenuTheme.Foreground,
        };
        _autostartItem.Click += (_, _) => ToggleAutostart();

        _exitItem = new ToolStripMenuItem("Exit")
        {
            ForeColor = MenuTheme.Foreground,
        };
        _exitItem.Click += (_, _) => ExitApplication();

        _menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = true,
            BackColor = MenuTheme.Background,
            ForeColor = MenuTheme.Foreground,
            Renderer = new EasyMonitoringMenuRenderer(),
        };
        _menu.Opening += (_, _) => RefreshAutostartStateIfStale();
        _menu.Items.AddRange(
        [
            _cpuItem,
            _gpuItem,
            _ramItem,
            new ToolStripSeparator(),
            _autostartItem,
            new ToolStripSeparator(),
            _exitItem,
        ]);

        _notifyIcon = new NotifyIcon
        {
            Icon = _frames[0],
            Text = "EasyMonitoring",
            ContextMenuStrip = _menu,
            Visible = showTrayIcon,
        };

        _animationTimer = new WinFormsTimer
        {
            Interval = AnimationIntervalCalculator.MaxIntervalMs,
        };
        _animationTimer.Tick += (_, _) => AdvanceAnimationFrame();
        _animationTimer.Start();

        _sampler = new MetricsSampler();
        _sampler.SnapshotReady += OnSnapshotReady;
        _sampler.Start();

        _ = Task.Run(RefreshAutostartState);
    }

    private static ToolStripMenuItem CreateInfoItem(string text)
    {
        return new ToolStripMenuItem(text)
        {
            Enabled = false,
            ForeColor = MenuTheme.Foreground,
        };
    }

    private void RefreshAutostartStateIfStale()
    {
        if (_autostartBusy)
        {
            return;
        }

        _ = Task.Run(RefreshAutostartState);
    }

    private void RefreshAutostartState()
    {
        if (_disposed)
        {
            return;
        }

        var state = _autostart.Refresh(DateTimeOffset.UtcNow);

        _uiContext.Post(_ =>
        {
            if (_disposed)
            {
                return;
            }

            _autostartItem.Checked = state.IsKnown ? state.IsEnabled : _autostartItem.Checked;
            _autostartItem.Enabled = true;
            AutostartStateLoaded = true;
        }, null);
    }

    private void ToggleAutostart()
    {
        if (_autostartBusy)
        {
            return;
        }

        _autostartBusy = true;
        _autostartItem.Enabled = false;

        var desired = !_autostartItem.Checked;
        var previous = _autostartItem.Checked;

        _ = Task.Run(() =>
        {
            var result = _autostart.SetEnabled(desired, DateTimeOffset.UtcNow);
            var state = _autostart.State;

            _uiContext.Post(_ =>
            {
                _autostartBusy = false;

                if (_disposed)
                {
                    return;
                }

                _autostartItem.Enabled = true;
                _autostartItem.Checked = state.IsKnown
                    ? state.IsEnabled
                    : result.Success
                        ? desired
                        : previous;
                AutostartStateLoaded = true;

                if (!result.Success)
                {
                    ShowAutostartError(result.Error);
                }
            }, null);
        });
    }

    private void ShowAutostartError(string? error)
    {
        AppLog.Error($"Failed to update autostart: {error}");

        if (_notifyIcon.Visible)
        {
            _notifyIcon.ShowBalloonTip(
                timeout: 5000,
                tipTitle: "EasyMonitoring",
                tipText: $"Could not update autostart: {error}",
                tipIcon: ToolTipIcon.Warning);
        }
    }

    private void OnSnapshotReady(MetricsSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        _uiContext.Post(_ => ApplySnapshot(snapshot), null);
    }

    private void ApplySnapshot(MetricsSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        AppliedSnapshotCount++;

        var cpuText = StatusFormatter.FormatCpu(snapshot.CpuLoadPercent, snapshot.CpuTemperatureC);
        _notifyIcon.Text = cpuText;
        _cpuItem.Text = cpuText;
        _gpuItem.Text = StatusFormatter.FormatGpu(snapshot.GpuLoadPercent, snapshot.GpuTemperatureC);
        _ramItem.Text = StatusFormatter.FormatRam(
            snapshot.RamUsedGb,
            snapshot.RamTotalGb,
            snapshot.RamUsedPercent);

        var interval = AnimationIntervalCalculator.Calculate(snapshot.CpuLoadPercent);
        if (_animationTimer.Interval != interval)
        {
            _animationTimer.Interval = interval;
        }
    }

    private void AdvanceAnimationFrame()
    {
        if (_disposed || _frames.Count == 0)
        {
            return;
        }

        _frameIndex = (_frameIndex + 1) % _frames.Count;
        _notifyIcon.Icon = _frames[_frameIndex];
    }

    private void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        DisposeResources();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeResources();
        }

        base.Dispose(disposing);
    }

    private void DisposeResources()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _animationTimer.Stop();
        _animationTimer.Dispose();

        _sampler.SnapshotReady -= OnSnapshotReady;
        _sampler.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();

        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
    }
}
