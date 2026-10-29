using EasyMonitoring.Core.Autostart;

namespace EasyMonitoring.Core.Tests;

public class AutostartReconciliationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Mutation_exception_still_rechecks_actual_state()
    {
        var manager = new FailingManager();
        var module = new AutostartStateModule(manager);

        var result = module.SetEnabled(true, Now);

        Assert.False(result.Success);
        Assert.Equal(2, manager.Probes);
        Assert.True(module.State.IsKnown);
        Assert.False(module.State.IsEnabled);
        Assert.Equal("mutation failed", result.Error);
    }

    [Fact]
    public void Unknown_presence_never_blindly_overwrites_a_task()
    {
        var manager = new FailingManager { Unknown = true };
        var module = new AutostartStateModule(manager);

        var result = module.SetEnabled(true, Now);

        Assert.False(result.Success);
        Assert.Equal(0, manager.Mutations);
        Assert.False(module.State.IsKnown);
    }

    [Fact]
    public void Owned_unsafe_task_is_removed_and_absence_is_verified()
    {
        var manager = new ScriptedManager(Existing(ExecutableProtection.Unsafe), Missing());
        var module = new AutostartStateModule(manager);

        var state = module.Refresh(Now, force: true);

        Assert.Equal(1, manager.Deletes);
        Assert.Equal(2, manager.Probes);
        Assert.False(state.IsEnabled);
        Assert.Equal(TaskPresence.Missing, state.Presence);
        Assert.NotNull(state.Notice);
    }

    [Fact]
    public void Diagnostic_inspection_never_removes_an_unsafe_user_task()
    {
        var manager = new ScriptedManager(Existing(ExecutableProtection.Unsafe));
        var module = new AutostartStateModule(manager);
        var state = module.Inspect(Now);
        Assert.Equal(TaskPresence.Present, state.Presence);
        Assert.Equal(0, manager.Deletes);
    }

    [Theory]
    [InlineData(TaskOwnership.OtherUser, ExecutableProtection.Unsafe)]
    [InlineData(TaskOwnership.Unknown, ExecutableProtection.Unsafe)]
    [InlineData(TaskOwnership.CurrentUser, ExecutableProtection.Unknown)]
    public void Foreign_or_uncertain_tasks_are_never_mutated(TaskOwnership owner, ExecutableProtection protection)
    {
        var manager = new ScriptedManager(Existing(protection) with { Ownership = owner });
        var module = new AutostartStateModule(manager);

        var result = module.SetEnabled(true, Now);

        Assert.False(result.Success);
        Assert.Equal(0, manager.Deletes + manager.Creates);
    }

    [Fact]
    public void Failed_unsafe_cleanup_remains_visible_and_does_not_loop()
    {
        var manager = new ScriptedManager(Existing(ExecutableProtection.Unsafe), Existing(ExecutableProtection.Unsafe))
        { MutationResult = new AutostartResult(false, "delete denied") };
        var module = new AutostartStateModule(manager);

        var state = module.Refresh(Now, force: true);

        Assert.Equal(1, manager.Deletes);
        Assert.Equal(2, manager.Probes);
        Assert.Equal(TaskPresence.Present, state.Presence);
        Assert.Contains("delete denied", state.LastError);
    }

    [Fact]
    public void Disabled_but_present_task_is_deleted_rather_than_treated_as_absent()
    {
        var manager = new ScriptedManager(Existing(ExecutableProtection.Safe) with { Enabled = false }, Missing());
        var module = new AutostartStateModule(manager);

        Assert.True(module.SetEnabled(false, Now).Success);
        Assert.Equal(1, manager.Deletes);
        Assert.Equal(2, manager.Probes);
    }

    [Fact]
    public void Normal_enable_has_exactly_one_pre_and_one_post_probe()
    {
        var manager = new ScriptedManager(Missing(), Existing(ExecutableProtection.Safe));
        var module = new AutostartStateModule(manager);

        Assert.True(module.SetEnabled(true, Now).Success);
        Assert.Equal(1, manager.Creates);
        Assert.Equal(2, manager.Probes);
    }

    private static AutostartProbe Missing() => new(true, false, Presence: TaskPresence.Missing);
    private static AutostartProbe Existing(ExecutableProtection protection) => new(
        true, true, new(true, true, true), Presence: TaskPresence.Present,
        Ownership: TaskOwnership.CurrentUser, Protection: protection);

    private sealed class ScriptedManager(params AutostartProbe[] observations) : IAutostartManager
    {
        public int Probes { get; private set; }
        public int Creates { get; private set; }
        public int Deletes { get; private set; }
        public AutostartResult MutationResult { get; init; } = new(true);
        public AutostartProbe Probe() => observations[Math.Min(Probes++, observations.Length - 1)];
        public AutostartResult Enable() { Creates++; return MutationResult; }
        public AutostartResult Disable() { Deletes++; return MutationResult; }
    }

    private sealed class FailingManager : IAutostartManager
    {
        public bool Unknown { get; init; }
        public int Probes { get; private set; }
        public int Mutations { get; private set; }

        public AutostartProbe Probe()
        {
            Probes++;
            return Unknown
                ? new AutostartProbe(false, false, Error: "access denied")
                : Missing();
        }

        public AutostartResult Enable()
        {
            Mutations++;
            throw new InvalidOperationException("mutation failed");
        }

        public AutostartResult Disable() => Enable();
    }
}
