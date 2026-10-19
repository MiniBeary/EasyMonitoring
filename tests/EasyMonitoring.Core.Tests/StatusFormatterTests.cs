using System.Globalization;
using EasyMonitoring.Core.Formatting;

namespace EasyMonitoring.Core.Tests;

public class StatusFormatterTests
{
    [Fact]
    public void FormatCpu_includes_load_and_temperature()
    {
        Assert.Equal("CPU: 23% | 58 °C", StatusFormatter.FormatCpu(23, 58));
    }

    [Fact]
    public void FormatCpu_uses_na_for_missing_temperature()
    {
        Assert.Equal("CPU: 23% | N/A", StatusFormatter.FormatCpu(23, null));
    }

    [Fact]
    public void FormatCpu_uses_na_for_missing_load()
    {
        Assert.Equal("CPU: N/A | 58 °C", StatusFormatter.FormatCpu(null, 58));
    }

    [Fact]
    public void FormatCpu_uses_na_when_both_values_are_missing()
    {
        Assert.Equal("CPU: N/A | N/A", StatusFormatter.FormatCpu(null, null));
    }

    [Fact]
    public void FormatCpu_rounds_values_to_whole_numbers()
    {
        Assert.Equal("CPU: 24% | 59 °C", StatusFormatter.FormatCpu(23.5, 58.5));
    }

    [Fact]
    public void FormatGpu_includes_load_and_temperature()
    {
        Assert.Equal("GPU: 5% | 47 °C", StatusFormatter.FormatGpu(5, 47));
    }

    [Fact]
    public void FormatGpu_uses_na_for_missing_values()
    {
        Assert.Equal("GPU: N/A | N/A", StatusFormatter.FormatGpu(null, null));
    }

    [Fact]
    public void FormatRam_includes_used_total_and_percent()
    {
        Assert.Equal("RAM: 8.2 / 31.9 GB (26%)", StatusFormatter.FormatRam(8.2, 31.9, 26));
    }

    [Fact]
    public void FormatRam_computes_percent_when_it_is_missing()
    {
        Assert.Equal("RAM: 8.0 / 32.0 GB (25%)", StatusFormatter.FormatRam(8, 32, null));
    }

    [Fact]
    public void FormatRam_uses_na_when_values_are_missing()
    {
        Assert.Equal("RAM: N/A", StatusFormatter.FormatRam(null, null, null));
    }

    [Fact]
    public void FormatRam_uses_dot_decimal_separator_regardless_of_current_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

            Assert.Equal("RAM: 8.2 / 31.9 GB (26%)", StatusFormatter.FormatRam(8.2, 31.9, 26));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
