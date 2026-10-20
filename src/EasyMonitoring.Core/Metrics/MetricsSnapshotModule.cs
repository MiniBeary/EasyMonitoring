namespace EasyMonitoring.Core.Metrics;

public sealed class MetricsSnapshotModule
{
    public static readonly TimeSpan DefaultSlowInterval = TimeSpan.FromSeconds(2);

    private readonly IMetricsSource _source;
    private readonly int _sampleWindow;
    private readonly TimeSpan _slowInterval;
    private readonly Queue<double> _cpuSamples = new();
    private readonly Queue<double> _gpuSamples = new();

    private DateTimeOffset? _lastSlowSampleAt;
    private double? _lastCpuTemperature;
    private double? _lastGpuTemperature;
    private MemorySample? _lastMemory;
    private int _cpuMisses;
    private int _cpuTemperatureMisses;
    private int _gpuLoadMisses;
    private int _gpuTemperatureMisses;
    private int _memoryMisses;

    public MetricsSnapshotModule(
        IMetricsSource source,
        int sampleWindow = 3,
        TimeSpan? slowInterval = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        if (sampleWindow <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleWindow), "Window size must be positive.");
        }

        _sampleWindow = sampleWindow;
        _slowInterval = slowInterval ?? DefaultSlowInterval;

        if (_slowInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(slowInterval), "Slow interval must be positive.");
        }
    }

    public MetricsSnapshot Poll(DateTimeOffset now)
    {
        AddCpuSample(TryReadCpu());

        if (IsSlowSampleDue(now))
        {
            _lastSlowSampleAt = now;

            var hardware = TryReadHardware();
            ApplyCpuTemperature(hardware?.CpuTemperatureC);
            ApplyGpuLoad(hardware?.GpuLoadPercent);
            ApplyGpuTemperature(hardware?.GpuTemperatureC);
            ApplyMemory(TryReadMemory());
        }

        return new MetricsSnapshot(
            CpuLoadPercent: Average(_cpuSamples),
            CpuTemperatureC: _lastCpuTemperature,
            GpuLoadPercent: Average(_gpuSamples),
            GpuTemperatureC: _lastGpuTemperature,
            RamUsedGb: _lastMemory?.UsedGb,
            RamTotalGb: _lastMemory?.TotalGb,
            RamUsedPercent: _lastMemory?.UsedPercent);
    }

    private bool IsSlowSampleDue(DateTimeOffset now)
    {
        return _lastSlowSampleAt is null || now - _lastSlowSampleAt.Value >= _slowInterval;
    }

    private double? TryReadCpu()
    {
        try
        {
            return _source.ReadCpuLoad();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private HardwareSample? TryReadHardware()
    {
        try
        {
            return _source.ReadHardware();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private MemorySample? TryReadMemory()
    {
        try
        {
            return _source.ReadMemory();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void AddCpuSample(double? value)
    {
        if (value is null)
        {
            if (_cpuMisses >= 1)
            {
                _cpuSamples.Clear();
            }

            _cpuMisses++;
            return;
        }

        _cpuMisses = 0;
        Enqueue(_cpuSamples, value.Value);
    }

    private void ApplyGpuLoad(double? value)
    {
        if (value is null)
        {
            if (_gpuLoadMisses >= 1)
            {
                _gpuSamples.Clear();
            }

            _gpuLoadMisses++;
            return;
        }

        _gpuLoadMisses = 0;
        Enqueue(_gpuSamples, value.Value);
    }

    private void ApplyCpuTemperature(double? value)
    {
        if (value is null)
        {
            if (_cpuTemperatureMisses >= 1)
            {
                _lastCpuTemperature = null;
            }

            _cpuTemperatureMisses++;
            return;
        }

        _cpuTemperatureMisses = 0;
        _lastCpuTemperature = value;
    }

    private void ApplyGpuTemperature(double? value)
    {
        if (value is null)
        {
            if (_gpuTemperatureMisses >= 1)
            {
                _lastGpuTemperature = null;
            }

            _gpuTemperatureMisses++;
            return;
        }

        _gpuTemperatureMisses = 0;
        _lastGpuTemperature = value;
    }

    private void ApplyMemory(MemorySample? value)
    {
        if (value is null)
        {
            if (_memoryMisses >= 1)
            {
                _lastMemory = null;
            }

            _memoryMisses++;
            return;
        }

        _memoryMisses = 0;
        _lastMemory = value;
    }

    private void Enqueue(Queue<double> samples, double value)
    {
        samples.Enqueue(value);

        while (samples.Count > _sampleWindow)
        {
            samples.Dequeue();
        }
    }

    private static double? Average(Queue<double> samples)
    {
        return samples.Count == 0 ? null : samples.Average();
    }
}
