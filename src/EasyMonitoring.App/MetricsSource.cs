using EasyMonitoring.App.Logging;
using EasyMonitoring.App.Native;
using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.App;

internal sealed class MetricsSource : IMetricsSource, IDisposable
{
    private readonly CpuLoadCalculator _cpuLoadCalculator = new();
    private readonly LhmMetricsSource _lhm = new();
    private bool _cpuErrorLogged;
    private bool _memoryErrorLogged;
    private bool _disposed;

    public double? ReadCpuLoad()
    {
        if (_disposed)
        {
            return null;
        }

        try
        {
            return _cpuLoadCalculator.AddSample(SystemTimes.Read());
        }
        catch (Exception exception)
        {
            if (!_cpuErrorLogged)
            {
                _cpuErrorLogged = true;
                AppLog.Error("Failed to read CPU times.", exception);
            }

            return null;
        }
    }

    public HardwareSample ReadHardware()
    {
        if (_disposed)
        {
            return new HardwareSample(null, null, null);
        }

        var reading = _lhm.Read();
        return new HardwareSample(
            reading.CpuTemperatureC,
            reading.GpuLoadPercent,
            reading.GpuTemperatureC);
    }

    public MemorySample? ReadMemory()
    {
        if (_disposed)
        {
            return null;
        }

        try
        {
            var status = GlobalMemoryStatusEx.Read();
            return new MemorySample(status.UsedGb, status.TotalGb, status.UsedPercent);
        }
        catch (Exception exception)
        {
            if (!_memoryErrorLogged)
            {
                _memoryErrorLogged = true;
                AppLog.Error("Failed to read memory status.", exception);
            }

            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lhm.Dispose();
    }
}
