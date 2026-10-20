namespace EasyMonitoring.Core.Sensors;

public static class SensorValuePolicy
{
    public static double? Temperature(double? value)
    {
        return value is > 0 and < 150 ? value : null;
    }

    public static double? Percent(double? value)
    {
        return value is >= 0 and <= 100 ? value : null;
    }
}
