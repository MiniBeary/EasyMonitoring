using EasyMonitoring.Core.Metrics;

namespace EasyMonitoring.Core.Tests;

public class MovingAverageTests
{
    [Fact]
    public void Add_returns_the_value_for_a_single_sample()
    {
        var average = new MovingAverage(3);

        Assert.Equal(10, average.Add(10));
    }

    [Fact]
    public void Add_returns_the_average_of_two_samples()
    {
        var average = new MovingAverage(3);
        average.Add(10);

        Assert.Equal(15, average.Add(20));
    }

    [Fact]
    public void Add_returns_the_average_of_a_full_window()
    {
        var average = new MovingAverage(3);
        average.Add(10);
        average.Add(20);

        Assert.Equal(20, average.Add(30));
    }

    [Fact]
    public void Add_evicts_the_oldest_sample_when_the_window_is_full()
    {
        var average = new MovingAverage(3);
        average.Add(10);
        average.Add(20);
        average.Add(30);

        Assert.Equal(30, average.Add(40));
    }

    [Fact]
    public void Add_ignores_null_samples()
    {
        var average = new MovingAverage(3);
        average.Add(10);
        average.Add(null);

        Assert.Equal(15, average.Add(20));
        Assert.Equal(2, average.Count);
    }

    [Fact]
    public void Add_returns_null_when_no_values_have_been_added()
    {
        var average = new MovingAverage(3);

        Assert.Null(average.Add(null));
    }

    [Fact]
    public void Reset_clears_all_samples()
    {
        var average = new MovingAverage(3);
        average.Add(10);
        average.Add(20);

        average.Reset();

        Assert.Null(average.Value);
        Assert.Equal(0, average.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_a_non_positive_window(int windowSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MovingAverage(windowSize));
    }
}
