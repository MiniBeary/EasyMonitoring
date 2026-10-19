namespace EasyMonitoring.Core.Autostart;

public interface IAutostartManager
{
    bool IsEnabled();

    AutostartResult Enable();

    AutostartResult Disable();
}
