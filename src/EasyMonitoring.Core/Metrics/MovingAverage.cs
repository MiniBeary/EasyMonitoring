namespace EasyMonitoring.Core.Metrics;

public sealed class MovingAverage
{
    private readonly Queue<double> _samples = new();

    public MovingAverage(int windowSize)
    {
        if (windowSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be positive.");
        }

        WindowSize = windowSize;
    }

    public int WindowSize { get; }

    public int Count => _samples.Count;

    public double? Value => _samples.Count == 0 ? null : _samples.Average();

    public double? Add(double? value)
    {
        if (value is null)
        {
            return Value;
        }

        _samples.Enqueue(value.Value);

        while (_samples.Count > WindowSize)
        {
            _samples.Dequeue();
        }

        return Value;
    }

    public void Reset()
    {
        _samples.Clear();
    }
}
