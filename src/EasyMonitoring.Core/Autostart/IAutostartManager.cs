namespace EasyMonitoring.Core.Autostart;

public interface IAutostartManager
{
    AutostartProbe Probe();

    AutostartResult Enable();

    AutostartResult Disable();
}