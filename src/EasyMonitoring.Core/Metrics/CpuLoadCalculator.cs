namespace EasyMonitoring.Core.Metrics;

public sealed class CpuLoadCalculator
{
    private CpuTimes? _previous;

    public double? AddSample(CpuTimes sample)
    {
        if (_previous is null)
        {
            _previous = sample;
            return null;
        }

        var previous = _previous.Value;
        _previous = sample;

        if (sample.Idle < previous.Idle ||
            sample.Kernel < previous.Kernel ||
            sample.User < previous.User)
        {
            return null;
        }

        var idleDelta = sample.Idle - previous.Idle;
        var kernelDelta = sample.Kernel - previous.Kernel;
        var userDelta = sample.User - previous.User;
        var totalDelta = kernelDelta + userDelta;

        if (totalDelta == 0)
        {
            return null;
        }

        if (idleDelta > totalDelta)
        {
            return 0;
        }

        var busyDelta = totalDelta - idleDelta;
        var load = (double)busyDelta / totalDelta * 100;

        return Math.Clamp(load, 0, 100);
    }

    public void Reset()
    {
        _previous = null;
    }
}
