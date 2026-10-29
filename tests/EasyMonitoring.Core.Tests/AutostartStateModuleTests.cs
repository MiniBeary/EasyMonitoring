using EasyMonitoring.Core.Autostart;

namespace EasyMonitoring.Core.Tests;

public class AutostartStateModuleTests
{
    private static readonly AutostartTaskHealth Health = new(true, true, true);
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Refresh_marks_known_state_and_health()
    {
        var manager = new FakeAutostartManager
        {
            ProbeResult = Known(true),
        };
        var module = new AutostartStateModule(manager);

        var state = module.Refresh(Now, force: true);

        Assert.True(state.IsKnown);
        Assert.True(state.IsEnabled);
        Assert.Same(Health, state.Health);
        Assert.Null(state.LastError);
        Assert.Equal(1, manager.ProbeCalls);
    }

    [Fact]
    public void Refresh_returns_cached_state_while_fresh()
    {
        var manager = new FakeAutostartManager();
        var module = new AutostartStateModule(manager);

        module.Refresh(Now, force: true);
        module.Refresh(Now.AddSeconds(5));

        Assert.Equal(1, manager.ProbeCalls);
    }

    [Fact]
    public void Refresh_reprobes_after_staleness()
    {
        var manager = new FakeAutostartManager();
        var module = new AutostartStateModule(manager);

        module.Refresh(Now, force: true);
        module.Refresh(Now.AddSeconds(11));

        Assert.Equal(2, manager.ProbeCalls);
    }

    [Fact]
    public void Refresh_preserves_unknown_state_and_error()
    {
        var manager = new FakeAutostartManager
        {
            ProbeResult = new AutostartProbe(false, false, Error: "access denied"),
        };
        var module = new AutostartStateModule(manager);

        var state = module.Refresh(Now, force: true);

        Assert.False(state.IsKnown);
        Assert.Null(state.Enabled);
        Assert.Equal("access denied", state.LastError);
    }

    [Fact]
    public void SetEnabled_is_a_noop_when_actual_state_matches()
    {
        var manager = new FakeAutostartManager
        {
            ProbeResult = Known(true),
        };
        var module = new AutostartStateModule(manager);

        var result = module.SetEnabled(true, Now);

        Assert.True(result.Success);
        Assert.Equal(0, manager.EnableCalls);
        Assert.Equal(0, manager.DisableCalls);
    }

    [Fact]
    public void SetEnabled_enables_and_returns_verified_state()
    {
        var manager = new FakeAutostartManager();
        var module = new AutostartStateModule(manager);

        var result = module.SetEnabled(true, Now);

        Assert.True(result.Success);
        Assert.Equal(1, manager.EnableCalls);
        Assert.True(module.State.IsKnown);
        Assert.True(module.State.IsEnabled);
    }

    [Fact]
    public void SetEnabled_surfaces_failure_and_rechecks_actual_state()
    {
        var manager = new FakeAutostartManager
        {
            EnableResult = new AutostartResult(false, "access denied"),
        };
        var module = new AutostartStateModule(manager);

        var result = module.SetEnabled(true, Now);

        Assert.False(result.Success);
        Assert.Equal("access denied", result.Error);
        Assert.True(module.State.IsKnown);
        Assert.False(module.State.IsEnabled);
    }

    [Fact]
    public void SetEnabled_allows_retry_from_unknown_state()
    {
        var manager = new FakeAutostartManager
        {
            ProbeResult = new AutostartProbe(false, false, Error: "temporary failure"),
        };
        var module = new AutostartStateModule(manager);

        Assert.False(module.SetEnabled(true, Now).Success);
        Assert.Equal(0, manager.EnableCalls);
        manager.ProbeResult = Known(false);
        var result = module.SetEnabled(true, Now);

        Assert.True(result.Success);
        Assert.Equal(1, manager.EnableCalls);
        Assert.True(module.State.IsKnown);
        Assert.True(module.State.IsEnabled);
    }

    [Fact]
    public void Probe_exception_is_reported_as_unknown()
    {
        var manager = new FakeAutostartManager { ThrowOnProbe = true };
        var module = new AutostartStateModule(manager);

        var state = module.Refresh(Now, force: true);

        Assert.False(state.IsKnown);
        Assert.Equal("probe failed", state.LastError);
    }

    private static AutostartProbe Known(bool enabled) => new(true, enabled, Health,
        Presence: enabled ? TaskPresence.Present : TaskPresence.Missing,
        Ownership: enabled ? TaskOwnership.CurrentUser : TaskOwnership.Unknown,
        Protection: enabled ? ExecutableProtection.Safe : ExecutableProtection.Unknown);

    private sealed class FakeAutostartManager : IAutostartManager
    {
        public AutostartProbe ProbeResult { get; set; } = Known(false);

        public AutostartResult EnableResult { get; set; } = new(true);

        public AutostartResult DisableResult { get; set; } = new(true);

        public bool ThrowOnProbe { get; set; }

        public int ProbeCalls { get; private set; }

        public int EnableCalls { get; private set; }

        public int DisableCalls { get; private set; }

        public AutostartProbe Probe()
        {
            ProbeCalls++;

            if (ThrowOnProbe)
            {
                throw new InvalidOperationException("probe failed");
            }

            return ProbeResult;
        }

        public AutostartResult Enable()
        {
            EnableCalls++;

            if (EnableResult.Success)
            {
                ProbeResult = Known(true);
            }

            return EnableResult;
        }

        public AutostartResult Disable()
        {
            DisableCalls++;

            if (DisableResult.Success)
            {
                ProbeResult = Known(false);
            }

            return DisableResult;
        }
    }
}
