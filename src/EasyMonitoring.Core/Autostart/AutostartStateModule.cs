namespace EasyMonitoring.Core.Autostart;

public sealed class AutostartStateModule
{
    private readonly IAutostartManager _manager;
    private readonly TimeSpan _staleAfter;
    private readonly object _gate = new();
    private AutostartState _state = new(null, DateTimeOffset.MinValue);

    public AutostartStateModule(IAutostartManager manager, TimeSpan? staleAfter = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _staleAfter = staleAfter ?? TimeSpan.FromSeconds(10);
        if (_staleAfter <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(staleAfter), "Staleness must be positive.");
    }

    public AutostartState State { get { lock (_gate) return _state; } }

    public AutostartState Inspect(DateTimeOffset now)
    {
        lock (_gate) return ReadLocked(now);
    }

    public AutostartState Refresh(DateTimeOffset now, bool force = false)
    {
        lock (_gate)
        {
            if (!force && _state.IsKnown && now - _state.LastCheckedAt < _staleAfter)
                return _state;
            return RefreshLocked(now);
        }
    }

    public AutostartResult SetEnabled(bool enabled, DateTimeOffset now)
    {
        lock (_gate)
        {
            var current = RefreshLocked(now);
            if (!current.IsKnown || current.Presence == TaskPresence.Unknown ||
                (current.Presence == TaskPresence.Present &&
                 (current.Ownership != TaskOwnership.CurrentUser ||
                  (enabled && current.Protection != ExecutableProtection.Safe))))
                return new(false, current.LastError ?? "Task ownership or executable safety could not be verified.");

            if ((!enabled && current.Presence == TaskPresence.Missing) ||
                (enabled && current.IsEnabled && current.Protection == ExecutableProtection.Safe))
                return new(true);

            var result = Mutate(enabled);
            var actual = ReadLocked(now);
            var reached = enabled
                ? result.Success && actual.IsKnown && actual.IsEnabled &&
                  actual.Presence == TaskPresence.Present && actual.Ownership == TaskOwnership.CurrentUser &&
                  actual.Protection == ExecutableProtection.Safe
                : actual.Presence == TaskPresence.Missing && actual.IsKnown;
            if (reached) return new(true);

            var error = result.Error ?? actual.LastError ?? "Autostart state could not be verified.";
            _state = actual with { LastError = error };
            return new(false, error);
        }
    }

    private AutostartState RefreshLocked(DateTimeOffset now)
    {
        var current = ReadLocked(now);
        if (current.Presence != TaskPresence.Present || current.Ownership != TaskOwnership.CurrentUser ||
            current.Protection != ExecutableProtection.Unsafe)
            return current;

        var removal = Mutate(false);
        var after = ReadLocked(now);
        _state = after.Presence == TaskPresence.Missing && after.IsKnown
            ? after with { Notice = "Unsafe autostart was removed. Copy EasyMonitoring to an administrator-protected folder before enabling it again." }
            : after with { LastError = $"Could not remove unsafe autostart: {removal.Error ?? after.LastError ?? "task is still present"}" };
        return _state;
    }

    private AutostartResult Mutate(bool enabled)
    {
        try { return enabled ? _manager.Enable() : _manager.Disable(); }
        catch (Exception exception) { return new(false, exception.Message); }
    }

    private AutostartState ReadLocked(DateTimeOffset now)
    {
        try
        {
            var probe = _manager.Probe();
            var known = probe.IsKnown && probe.Presence != TaskPresence.Unknown &&
                (probe.Presence == TaskPresence.Missing ||
                 (probe.Ownership == TaskOwnership.CurrentUser && probe.Protection != ExecutableProtection.Unknown));
            _state = new(known ? probe.Enabled : null, now,
                probe.Error ?? (known ? null : "Task ownership or executable safety is unavailable."),
                probe.Health, probe.Presence, probe.Ownership, probe.Protection);
        }
        catch (Exception exception) { _state = new(null, now, exception.Message); }
        return _state;
    }
}
