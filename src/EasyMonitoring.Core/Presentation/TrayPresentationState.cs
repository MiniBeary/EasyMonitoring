using EasyMonitoring.Core.Autostart;
using EasyMonitoring.Core.Formatting;
using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.Core.Presentation;

public sealed class TrayPresentationState
{
    public const int MinIntervalMs = 50;
    public const int MaxIntervalMs = 500;

    public TrayPresentationState(int frameCount)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be positive.");
        }

        FrameCount = frameCount;
        TrayText = "EasyMonitoring";
        CpuText = StatusFormatter.FormatCpu(null, null);
        GpuText = StatusFormatter.FormatGpu(null, null);
        RamText = StatusFormatter.FormatRam(null, null, null);
        AnimationIntervalMs = MaxIntervalMs;
    }

    public int FrameCount { get; }

    public int CurrentFrameIndex { get; private set; }

    public int AnimationIntervalMs { get; private set; }

    public int AppliedSnapshotCount { get; private set; }

    public string TrayText { get; private set; }

    public string CpuText { get; private set; }

    public string GpuText { get; private set; }

    public string RamText { get; private set; }

    public bool AutostartChecked { get; private set; }

    public bool AutostartEnabled { get; private set; }

    public bool AutostartStateLoaded { get; private set; }

    public string? AutostartError { get; private set; }

    public void ApplySnapshot(MetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        AppliedSnapshotCount++;
        CpuText = StatusFormatter.FormatCpu(snapshot.CpuLoadPercent, snapshot.CpuTemperatureC);
        GpuText = StatusFormatter.FormatGpu(snapshot.GpuLoadPercent, snapshot.GpuTemperatureC);
        RamText = StatusFormatter.FormatRam(
            snapshot.RamUsedGb,
            snapshot.RamTotalGb,
            snapshot.RamUsedPercent);
        TrayText = CpuText;
        AnimationIntervalMs = CalculateInterval(snapshot.CpuLoadPercent);
    }

    public int AdvanceFrame()
    {
        CurrentFrameIndex = (CurrentFrameIndex + 1) % FrameCount;
        return CurrentFrameIndex;
    }

    public void ApplyAutostart(AutostartState state, bool? fallbackChecked = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        AutostartChecked = state.IsKnown
            ? state.IsEnabled
            : fallbackChecked ?? AutostartChecked;
        AutostartEnabled = true;
        AutostartStateLoaded = true;
        AutostartError = state.LastError;
    }

    public void SetAutostartEnabled(bool enabled)
    {
        AutostartEnabled = enabled;
    }

    public static int CalculateInterval(double? cpuLoadPercent)
    {
        if (cpuLoadPercent is null)
        {
            return MaxIntervalMs;
        }

        var load = Math.Clamp(cpuLoadPercent.Value, 0, 100);
        var interval = Math.Round(MaxIntervalMs - 4.5 * load, MidpointRounding.AwayFromZero);

        return (int)Math.Clamp(interval, MinIntervalMs, MaxIntervalMs);
    }
}
