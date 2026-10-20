namespace EasyMonitoring.Core.Autostart;

public sealed class AutostartStateModule
{
    private static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromSeconds(10);

    private readonly IAutostartManager _manager;
    private readonly TimeSpan _staleAfter;
    private readonly object _gate = new();
    private AutostartState _state = new(null, DateTimeOffset.MinValue);

    public AutostartStateModule(IAutostartManager manager, TimeSpan? staleAfter = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _staleAfter = staleAfter ?? DefaultStaleAfter;

        if (_staleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter), "Staleness must be positive.");
        }
    }

    public AutostartState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public AutostartState Refresh(DateTimeOffset now, bool force = false)
    {
        lock (_gate)
        {
            if (!force && _state.IsKnown && now - _state.LastCheckedAt < _staleAfter)
            {
                return _state;
            }

            return RefreshLocked(now);
        }
    }

    public AutostartResult SetEnabled(bool enabled, DateTimeOffset now)
    {
        lock (_gate)
        {
            var current = RefreshLocked(now);

            if (current.IsKnown && current.IsEnabled == enabled)
            {
                return new AutostartResult(true);
            }

            AutostartResult result;

            try
            {
                result = enabled ? _manager.Enable() : _manager.Disable();
            }
            catch (Exception exception)
            {
                _state = new AutostartState(null, now, exception.Message);
                return new AutostartResult(false, exception.Message);
            }

            var actual = RefreshLocked(now);

            if (result.Success && actual.IsKnown && actual.IsEnabled == enabled)
            {
                return new AutostartResult(true);
            }

            return new AutostartResult(
                false,
                result.Error ?? actual.LastError ?? "Autostart state could not be verified.");
        }
    }

    private AutostartState RefreshLocked(DateTimeOffset now)
    {
        try
        {
            var probe = _manager.Probe();

            _state = probe.IsKnown
                ? new AutostartState(probe.Enabled, now, null, probe.Health)
                : new AutostartState(null, now, probe.Error ?? "Autostart state is unavailable.", probe.Health);
        }
        catch (Exception exception)
        {
            _state = new AutostartState(null, now, exception.Message);
        }

        return _state;
    }
}
