namespace EasyMonitoring.Core.Autostart;

public sealed class AutostartController
{
    private readonly IAutostartManager _manager;

    public AutostartController(IAutostartManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public bool IsEnabled => _manager.IsEnabled();

    public AutostartResult SetEnabled(bool enabled)
    {
        if (_manager.IsEnabled() == enabled)
        {
            return new AutostartResult(true);
        }

        return enabled ? _manager.Enable() : _manager.Disable();
    }
}
