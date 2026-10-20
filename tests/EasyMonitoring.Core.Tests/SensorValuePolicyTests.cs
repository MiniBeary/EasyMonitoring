using EasyMonitoring.Core.Sensors;

namespace EasyMonitoring.Core.Tests;

public class SensorValuePolicyTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(0d, null)]
    [InlineData(25d, 25d)]
    [InlineData(149.9d, 149.9d)]
    [InlineData(150d, null)]
    [InlineData(-1d, null)]
    public void Temperature_only_accepts_the_documented_range(double? input, double? expected)
    {
        Assert.Equal(expected, SensorValuePolicy.Temperature(input));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(-1d, null)]
    [InlineData(0d, 0d)]
    [InlineData(100d, 100d)]
    [InlineData(101d, null)]
    public void Percent_only_accepts_zero_to_one_hundred(double? input, double? expected)
    {
        Assert.Equal(expected, SensorValuePolicy.Percent(input));
    }
}