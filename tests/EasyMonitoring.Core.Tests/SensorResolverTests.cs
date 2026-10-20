using EasyMonitoring.Core.Sensors;

namespace EasyMonitoring.Core.Tests;

public class SensorResolverTests
{
    [Fact]
    public void Resolve_prefers_tctl_tdie_for_cpu_temperature()
    {
        var cpu = Cpu(
            Temperature(1, "Core #1"),
            Temperature(2, "CPU Package"),
            Temperature(3, "Core (Tctl/Tdie)"));

        var selection = SensorResolver.Resolve(cpu, Array.Empty<HardwareDescriptor>());

        Assert.Equal(new SensorId(3), selection.CpuTemperature);
    }

    [Fact]
    public void Resolve_falls_back_to_cpu_package()
    {
        var cpu = Cpu(
            Temperature(1, "Core #1"),
            Temperature(2, "CPU Package"));

        var selection = SensorResolver.Resolve(cpu, Array.Empty<HardwareDescriptor>());

        Assert.Equal(new SensorId(2), selection.CpuTemperature);
    }

    [Fact]
    public void Resolve_prefers_tdie_before_core_sensors()
    {
        var cpu = Cpu(
            Temperature(1, "Core #1"),
            Temperature(2, "CCD1 (Tdie)"));

        var selection = SensorResolver.Resolve(cpu, Array.Empty<HardwareDescriptor>());

        Assert.Equal(new SensorId(2), selection.CpuTemperature);
    }

    [Fact]
    public void Resolve_does_not_use_an_unrelated_temperature_as_cpu_temperature()
    {
        var cpu = Cpu(
            Load(1, "CPU Total"),
            Temperature(2, "Motherboard"));

        var selection = SensorResolver.Resolve(cpu, Array.Empty<HardwareDescriptor>());

        Assert.Null(selection.CpuTemperature);
    }

    [Fact]
    public void Resolve_prefers_nvidia_gpu_over_amd_and_intel()
    {
        var amd = Gpu(20, HardwareKind.GpuAmd, Load(1, "GPU Core"));
        var nvidia = Gpu(21, HardwareKind.GpuNvidia, Load(2, "GPU Core"));
        var intel = Gpu(22, HardwareKind.GpuIntel, Load(3, "GPU Core"));

        var selection = SensorResolver.Resolve(null, new[] { amd, nvidia, intel });

        Assert.Equal(nvidia.Id, selection.GpuHardware);
        Assert.Equal(new SensorId(2), selection.GpuLoad);
        Assert.Null(selection.GpuTemperature);
    }

    [Fact]
    public void Resolve_skips_a_preferred_gpu_without_relevant_sensors()
    {
        var nvidia = Gpu(20, HardwareKind.GpuNvidia);
        var amd = Gpu(21, HardwareKind.GpuAmd, Temperature(1, "GPU Core"));

        var selection = SensorResolver.Resolve(null, new[] { nvidia, amd });

        Assert.Equal(amd.Id, selection.GpuHardware);
        Assert.Null(selection.GpuLoad);
        Assert.Equal(new SensorId(1), selection.GpuTemperature);
    }

    [Fact]
    public void Resolve_ignores_non_gpu_hardware()
    {
        var gpu = Gpu(20, HardwareKind.Other, Temperature(1, "GPU Core"));

        var selection = SensorResolver.Resolve(null, new[] { gpu });

        Assert.Null(selection.GpuHardware);
        Assert.Null(selection.GpuLoad);
        Assert.Null(selection.GpuTemperature);
    }

    [Fact]
    public void Resolve_returns_null_gpu_when_no_gpu_has_relevant_sensors()
    {
        var nvidia = Gpu(20, HardwareKind.GpuNvidia);

        var selection = SensorResolver.Resolve(null, new[] { nvidia });

        Assert.Null(selection.GpuHardware);
    }

    [Fact]
    public void Resolve_prefers_gpu_core_load_and_temperature_and_falls_back()
    {
        var gpu = Gpu(
            20,
            HardwareKind.GpuNvidia,
            Load(1, "GPU Memory"),
            Load(2, "GPU Core"),
            Temperature(3, "GPU Hot Spot"),
            Temperature(4, "GPU Core"));

        var selection = SensorResolver.Resolve(null, new[] { gpu });

        Assert.Equal(new SensorId(2), selection.GpuLoad);
        Assert.Equal(new SensorId(4), selection.GpuTemperature);
    }

    [Fact]
    public void Resolve_falls_back_to_any_gpu_load_and_temperature()
    {
        var gpu = Gpu(
            20,
            HardwareKind.GpuAmd,
            Load(1, "D3D"),
            Temperature(2, "GPU Hot Spot"));

        var selection = SensorResolver.Resolve(null, new[] { gpu });

        Assert.Equal(new SensorId(1), selection.GpuLoad);
        Assert.Equal(new SensorId(2), selection.GpuTemperature);
    }

    private static SensorDescriptor Temperature(int id, string name) =>
        new(new SensorId(id), name, SensorKind.Temperature);

    private static SensorDescriptor Load(int id, string name) =>
        new(new SensorId(id), name, SensorKind.Load);

    private static HardwareDescriptor Cpu(params SensorDescriptor[] sensors) =>
        new(new HardwareId(1), HardwareKind.Cpu, "CPU", sensors);

    private static HardwareDescriptor Gpu(
        int hardwareId,
        HardwareKind kind,
        params SensorDescriptor[] sensors) =>
        new(new HardwareId(hardwareId), kind, kind.ToString(), sensors);
}
