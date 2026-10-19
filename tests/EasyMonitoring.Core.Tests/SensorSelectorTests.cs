using EasyMonitoring.Core.Sensors;

namespace EasyMonitoring.Core.Tests;

public class SensorSelectorTests
{
    [Fact]
    public void SelectCpuTemperature_prefers_tctl_tdie()
    {
        var sensors = new[]
        {
            Temperature("Core #1", 60),
            Temperature("CPU Package", 62),
            Temperature("Core (Tctl/Tdie)", 58),
        };

        var selected = SensorSelector.SelectCpuTemperature(sensors);

        Assert.Equal("Core (Tctl/Tdie)", selected?.Name);
    }

    [Fact]
    public void SelectCpuTemperature_falls_back_to_cpu_package()
    {
        var sensors = new[]
        {
            Temperature("Core #1", 60),
            Temperature("CPU Package", 62),
        };

        var selected = SensorSelector.SelectCpuTemperature(sensors);

        Assert.Equal("CPU Package", selected?.Name);
    }

    [Fact]
    public void SelectCpuTemperature_prefers_tdie_named_sensors_before_core_sensors()
    {
        var sensors = new[]
        {
            Temperature("Core #1", 60),
            Temperature("CCD1 (Tdie)", 64),
        };

        var selected = SensorSelector.SelectCpuTemperature(sensors);

        Assert.Equal("CCD1 (Tdie)", selected?.Name);
    }

    [Fact]
    public void SelectCpuTemperature_falls_back_to_any_temperature_sensor()
    {
        var sensors = new[]
        {
            Load("CPU Total", 30),
            Temperature("Motherboard", 35),
        };

        var selected = SensorSelector.SelectCpuTemperature(sensors);

        Assert.Equal("Motherboard", selected?.Name);
    }

    [Fact]
    public void SelectCpuTemperature_returns_null_when_no_temperature_exists()
    {
        var sensors = new[] { Load("CPU Total", 30) };

        Assert.Null(SensorSelector.SelectCpuTemperature(sensors));
    }

    [Fact]
    public void SelectGpu_prefers_nvidia_over_amd_and_intel()
    {
        var amd = Gpu("GpuAmd", Load("GPU Core", 10));
        var nvidia = Gpu("GpuNvidia", Load("GPU Core", 20));
        var intel = Gpu("GpuIntel", Load("GPU Core", 30));

        var selected = SensorSelector.SelectGpu(new[] { amd, nvidia, intel });

        Assert.Same(nvidia, selected);
    }

    [Fact]
    public void SelectGpu_skips_a_preferred_gpu_without_relevant_sensors()
    {
        var nvidia = Gpu("GpuNvidia");
        var amd = Gpu("GpuAmd", Temperature("GPU Core", 47));

        var selected = SensorSelector.SelectGpu(new[] { nvidia, amd });

        Assert.Same(amd, selected);
    }

    [Fact]
    public void SelectGpu_returns_null_when_no_gpu_has_relevant_sensors()
    {
        var nvidia = Gpu("GpuNvidia");

        Assert.Null(SensorSelector.SelectGpu(new[] { nvidia }));
    }

    [Fact]
    public void SelectGpu_returns_null_when_no_gpu_exists()
    {
        Assert.Null(SensorSelector.SelectGpu(Array.Empty<HardwareReadings>()));
    }

    [Fact]
    public void SelectGpuLoad_prefers_gpu_core()
    {
        var gpu = Gpu("GpuNvidia", Load("GPU Memory", 10), Load("GPU Core", 20));

        Assert.Equal("GPU Core", SensorSelector.SelectGpuLoad(gpu)?.Name);
    }

    [Fact]
    public void SelectGpuLoad_falls_back_to_any_load_sensor()
    {
        var gpu = Gpu("GpuNvidia", Load("D3D", 10));

        Assert.Equal("D3D", SensorSelector.SelectGpuLoad(gpu)?.Name);
    }

    [Fact]
    public void SelectGpuLoad_returns_null_when_no_load_sensor_exists()
    {
        var gpu = Gpu("GpuNvidia", Temperature("GPU Core", 47));

        Assert.Null(SensorSelector.SelectGpuLoad(gpu));
    }

    [Fact]
    public void SelectGpuTemperature_prefers_gpu_core()
    {
        var gpu = Gpu("GpuNvidia", Temperature("GPU Hot Spot", 60), Temperature("GPU Core", 47));

        Assert.Equal("GPU Core", SensorSelector.SelectGpuTemperature(gpu)?.Name);
    }

    [Fact]
    public void SelectGpuTemperature_falls_back_to_any_temperature_sensor()
    {
        var gpu = Gpu("GpuNvidia", Temperature("GPU Hot Spot", 60));

        Assert.Equal("GPU Hot Spot", SensorSelector.SelectGpuTemperature(gpu)?.Name);
    }

    [Fact]
    public void SelectGpuTemperature_returns_null_when_no_temperature_sensor_exists()
    {
        var gpu = Gpu("GpuNvidia", Load("GPU Core", 20));

        Assert.Null(SensorSelector.SelectGpuTemperature(gpu));
    }

    private static SensorReading Temperature(string name, double value) =>
        new(name, SensorKind.Temperature, value);

    private static SensorReading Load(string name, double value) =>
        new(name, SensorKind.Load, value);

    private static HardwareReadings Gpu(string type, params SensorReading[] sensors) =>
        new(type, type, sensors);
}
