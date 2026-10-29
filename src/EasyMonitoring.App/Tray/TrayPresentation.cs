using EasyMonitoring.App.Icons;
using EasyMonitoring.App.Logging;
using EasyMonitoring.App.UI;
using EasyMonitoring.Core.Autostart;
using EasyMonitoring.Core.Metrics;
using EasyMonitoring.Core.Presentation;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace EasyMonitoring.App.Tray;

internal sealed record TrayDiagnostics(
    int Snapshots,
    string TrayText,
    IReadOnlyList<string> MenuTexts,
    int AnimationIntervalMs,
    int AnimationFrameCount,
    int CurrentFrameIndex,
    bool AutostartChecked,
    bool AutostartStateLoaded,
    Color MenuBackColor,
    Color MenuForeColor,
    bool ShowCheckMargin);

internal sealed class TrayPresentation : IDisposable
{
    private readonly IReadOnlyList<Icon> _frames;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _cpuItem;
    private readonly ToolStripMenuItem _gpuItem;
    private readonly ToolStripMenuItem _ramItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly WinFormsTimer _animationTimer;
    private readonly TrayPresentationState _state;
    private bool _disposed;

    public TrayPresentation(bool showTrayIcon = true)
    {
        _frames = ArcIconFactory.CreateFrames();
        _state = new TrayPresentationState(_frames.Count);

        _cpuItem = CreateInfoItem(_state.CpuText);
        _gpuItem = CreateInfoItem(_state.GpuText);
        _ramItem = CreateInfoItem(_state.RamText);
        _autostartItem = new ToolStripMenuItem("Start with Windows")
        {
            Enabled = false,
            ForeColor = MenuTheme.Foreground,
        };
        _autostartItem.Click += (_, _) => AutostartToggleRequested?.Invoke(this, EventArgs.Empty);
        _exitItem = new ToolStripMenuItem("Exit")
        {
            ForeColor = MenuTheme.Foreground,
        };
        _exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = true,
            BackColor = MenuTheme.Background,
            ForeColor = MenuTheme.Foreground,
            Renderer = new EasyMonitoringMenuRenderer(),
        };
        _menu.Opening += (_, _) => MenuOpening?.Invoke(this, EventArgs.Empty);
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
            Text = _state.TrayText,
            ContextMenuStrip = _menu,
            Visible = showTrayIcon,
        };

        _animationTimer = new WinFormsTimer
        {
            Interval = _state.AnimationIntervalMs,
        };
        _animationTimer.Tick += (_, _) => AdvanceAnimationFrame();
        _animationTimer.Start();
    }

    public event EventHandler? AutostartToggleRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? MenuOpening;

    public bool AutostartChecked => _autostartItem.Checked;

    public void ApplySnapshot(MetricsSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        _state.ApplySnapshot(snapshot);
        _notifyIcon.Text = _state.TrayText;
        _cpuItem.Text = _state.CpuText;
        _gpuItem.Text = _state.GpuText;
        _ramItem.Text = _state.RamText;

        if (_animationTimer.Interval != _state.AnimationIntervalMs)
        {
            _animationTimer.Interval = _state.AnimationIntervalMs;
        }
    }

    public void ApplyAutostart(AutostartState state, bool? fallbackChecked = null)
    {
        if (_disposed)
        {
            return;
        }

        _state.ApplyAutostart(state, fallbackChecked);
        _autostartItem.Checked = _state.AutostartChecked;
        _autostartItem.Enabled = _state.AutostartEnabled;
    }

    public void SetAutostartEnabled(bool enabled)
    {
        _state.SetAutostartEnabled(enabled);
        _autostartItem.Enabled = enabled;
    }

    public void ShowAutostartError(string? error)
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

    public void ShowAutostartNotice(string message)
    {
        AppLog.Warning(message);
        if (_notifyIcon.Visible)
            _notifyIcon.ShowBalloonTip(5000, "EasyMonitoring", message, ToolTipIcon.Warning);
    }

    public TrayDiagnostics GetDiagnostics()
    {
        return new TrayDiagnostics(
            Snapshots: _state.AppliedSnapshotCount,
            TrayText: _notifyIcon.Text,
            MenuTexts: _menu.Items
                .OfType<ToolStripMenuItem>()
                .Select(item => item.Text ?? string.Empty)
                .ToArray(),
            AnimationIntervalMs: _animationTimer.Interval,
            AnimationFrameCount: _frames.Count,
            CurrentFrameIndex: _state.CurrentFrameIndex,
            AutostartChecked: _autostartItem.Checked,
            AutostartStateLoaded: _state.AutostartStateLoaded,
            MenuBackColor: _menu.BackColor,
            MenuForeColor: _menu.ForeColor,
            ShowCheckMargin: _menu.ShowCheckMargin);
    }

    private void AdvanceAnimationFrame()
    {
        if (_disposed || _frames.Count == 0)
        {
            return;
        }

        var frameIndex = _state.AdvanceFrame();
        _notifyIcon.Icon = _frames[frameIndex];
    }

    private static ToolStripMenuItem CreateInfoItem(string text)
    {
        return new ToolStripMenuItem(text)
        {
            Enabled = false,
            ForeColor = MenuTheme.Foreground,
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _animationTimer.Stop();
        _animationTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();

        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
    }
}
