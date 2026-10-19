namespace EasyMonitoring.Core.Animation;

public static class AnimationIntervalCalculator
{
    public const int MinIntervalMs = 50;
    public const int MaxIntervalMs = 500;

    public static int Calculate(double? cpuLoadPercent)
    {
        if (cpuLoadPercent is null)
        {
            return MaxIntervalMs;
        }

        var load = Math.Clamp(cpuLoadPercent.Value, 0, 100);
        var interval = Math.Round(MaxIntervalMs - 4.5 * load, MidpointRounding.AwayFromZero);

        return (int)Math.Clamp(interval, MinIntervalMs, MaxIntervalMs);
    }
}
