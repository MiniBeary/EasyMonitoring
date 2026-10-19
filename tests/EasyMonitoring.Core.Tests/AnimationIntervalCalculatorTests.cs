using EasyMonitoring.Core.Animation;

namespace EasyMonitoring.Core.Tests;

public class AnimationIntervalCalculatorTests
{
    [Theory]
    [InlineData(0, 500)]
    [InlineData(25, 388)]
    [InlineData(50, 275)]
    [InlineData(75, 163)]
    [InlineData(100, 50)]
    [InlineData(-10, 500)]
    [InlineData(150, 50)]
    public void Calculate_maps_cpu_load_to_animation_interval(double load, int expected)
    {
        Assert.Equal(expected, AnimationIntervalCalculator.Calculate(load));
    }

    [Fact]
    public void Calculate_uses_slowest_animation_when_load_is_unknown()
    {
        Assert.Equal(500, AnimationIntervalCalculator.Calculate(null));
    }
}
