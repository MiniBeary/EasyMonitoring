using EasyMonitoring.Core.Autostart;
using EasyMonitoring.Core.Metrics;
using EasyMonitoring.Core.Presentation;

namespace EasyMonitoring.Core.Tests;

public class TrayPresentationStateTests
{
    [Fact]
    public void Known_state_does_not_hide_a_failed_safety_cleanup()
    {
        var state = new TrayPresentationState(5);
        state.ApplyAutostart(new AutostartState(false, DateTimeOffset.UtcNow, "unsafe task removal failed"));
        Assert.Equal("unsafe task removal failed", state.AutostartError);
    }

    [Fact]
    public void Initial_state_uses_na_text_and_slow_animation()
    {
        var state = new TrayPresentationState(frameCount: 5);

        Assert.Equal("EasyMonitoring", state.TrayText);
        Assert.Equal("CPU: N/A | N/A", state.CpuText);
        Assert.Equal(500, state.AnimationIntervalMs);
        Assert.Equal(0, state.CurrentFrameIndex);
        Assert.Equal(0, state.AppliedSnapshotCount);
    }

    [Fact]
    public void ApplySnapshot_formats_text_and_interval()
    {
        var state = new TrayPresentationState(frameCount: 5);
        var snapshot = new MetricsSnapshot(50, 60, 20, 47, 8.2, 31.9, 26);

        state.ApplySnapshot(snapshot);

        Assert.Equal("CPU: 50% | 60 °C", state.CpuText);
        Assert.Equal("GPU: 20% | 47 °C", state.GpuText);
        Assert.Equal("RAM: 8.2 / 31.9 GB (26%)", state.RamText);
        Assert.Equal(state.CpuText, state.TrayText);
        Assert.Equal(275, state.AnimationIntervalMs);
        Assert.Equal(1, state.AppliedSnapshotCount);
    }

    [Fact]
    public void AdvanceFrame_wraps_at_frame_count()
    {
        var state = new TrayPresentationState(frameCount: 5);

        Assert.Equal(1, state.AdvanceFrame());
        Assert.Equal(2, state.AdvanceFrame());
        Assert.Equal(3, state.AdvanceFrame());
        Assert.Equal(4, state.AdvanceFrame());
        Assert.Equal(0, state.AdvanceFrame());
    }

    [Fact]
    public void ApplyAutostart_uses_known_state()
    {
        var state = new TrayPresentationState(frameCount: 5);
        var autostart = new AutostartState(true, DateTimeOffset.UtcNow);

        state.ApplyAutostart(autostart);

        Assert.True(state.AutostartChecked);
        Assert.True(state.AutostartEnabled);
        Assert.True(state.AutostartStateLoaded);
        Assert.Null(state.AutostartError);
    }

    [Fact]
    public void ApplyAutostart_uses_fallback_when_state_is_unknown()
    {
        var state = new TrayPresentationState(frameCount: 5);
        var autostart = new AutostartState(null, DateTimeOffset.UtcNow, "probe failed");

        state.ApplyAutostart(autostart, fallbackChecked: true);

        Assert.True(state.AutostartChecked);
        Assert.True(state.AutostartEnabled);
        Assert.True(state.AutostartStateLoaded);
        Assert.Equal("probe failed", state.AutostartError);
    }

    [Fact]
    public void CalculateInterval_clamps_to_range()
    {
        Assert.Equal(500, TrayPresentationState.CalculateInterval(null));
        Assert.Equal(500, TrayPresentationState.CalculateInterval(0));
        Assert.Equal(50, TrayPresentationState.CalculateInterval(100));
        Assert.Equal(275, TrayPresentationState.CalculateInterval(50));
    }
}
