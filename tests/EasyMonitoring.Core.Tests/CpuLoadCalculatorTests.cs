using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.Core.Tests;

public class CpuLoadCalculatorTests
{
    [Fact]
    public void AddSample_returns_null_for_the_first_sample()
    {
        var calculator = new CpuLoadCalculator();

        Assert.Null(calculator.AddSample(new CpuTimes(100, 100, 100)));
    }

    [Fact]
    public void AddSample_returns_zero_for_an_idle_cpu()
    {
        var calculator = new CpuLoadCalculator();
        calculator.AddSample(new CpuTimes(100, 100, 100));

        var load = calculator.AddSample(new CpuTimes(200, 200, 100));

        Assert.Equal(0, load);
    }

    [Fact]
    public void AddSample_returns_one_hundred_for_a_fully_busy_cpu()
    {
        var calculator = new CpuLoadCalculator();
        calculator.AddSample(new CpuTimes(100, 100, 100));

        var load = calculator.AddSample(new CpuTimes(100, 200, 200));

        Assert.Equal(100, load);
    }

    [Fact]
    public void AddSample_subtracts_idle_time_from_total_time()
    {
        var calculator = new CpuLoadCalculator();
        calculator.AddSample(new CpuTimes(0, 0, 0));

        var load = calculator.AddSample(new CpuTimes(50, 100, 0));

        Assert.Equal(50, load);
    }

    [Fact]
    public void AddSample_calculates_a_mixed_load()
    {
        var calculator = new CpuLoadCalculator();
        calculator.AddSample(new CpuTimes(0, 0, 0));

        var load = calculator.AddSample(new CpuTimes(30, 80, 20));

        Assert.Equal(70, load);
    }

    [Fact]
    public void AddSample_returns_null_when_counters_move_backwards()
    {
        var calculator = new CpuLoadCalculator();
        calculator.AddSample(new CpuTimes(100, 100, 100));

        var load = calculator.AddSample(new CpuTimes(90, 110, 100));

        Assert.Null(load);
    }

    [Fact]
    public void AddSample_uses_the_invalid_sample_as_the_next_baseline()
    {
        var calculator = new CpuLoadCalculator();
        calculator.AddSample(new CpuTimes(100, 100, 100));
        calculator.AddSample(new CpuTimes(90, 110, 100));

        var load = calculator.AddSample(new CpuTimes(90, 160, 150));

        Assert.Equal(100, load);
    }

    [Fact]
    public void Reset_forces_the_next_sample_to_be_a_baseline()
    {
        var calculator = new CpuLoadCalculator();
        calculator.AddSample(new CpuTimes(0, 0, 0));
        calculator.Reset();

        Assert.Null(calculator.AddSample(new CpuTimes(100, 200, 200)));
    }
}
