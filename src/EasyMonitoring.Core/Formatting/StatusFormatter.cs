using System.Globalization;

namespace EasyMonitoring.Core.Formatting;

public static class StatusFormatter
{
    private const string NotAvailable = "N/A";

    public static string FormatCpu(double? loadPercent, double? temperatureC)
    {
        return $"CPU: {FormatPercent(loadPercent)} | {FormatTemperature(temperatureC)}";
    }

    public static string FormatGpu(double? loadPercent, double? temperatureC)
    {
        return $"GPU: {FormatPercent(loadPercent)} | {FormatTemperature(temperatureC)}";
    }

    public static string FormatRam(double? usedGb, double? totalGb, double? usedPercent)
    {
        if (usedGb is null || totalGb is null || totalGb <= 0)
        {
            return "RAM: N/A";
        }

        var percent = usedPercent ?? usedGb.Value / totalGb.Value * 100;

        return $"RAM: {FormatOneDecimal(usedGb.Value)} / {FormatOneDecimal(totalGb.Value)} GB ({FormatWhole(percent)}%)";
    }

    private static string FormatPercent(double? value)
    {
        return value is null ? NotAvailable : $"{FormatWhole(value.Value)}%";
    }

    private static string FormatTemperature(double? value)
    {
        return value is null ? NotAvailable : $"{FormatWhole(value.Value)} °C";
    }

    private static string FormatWhole(double value)
    {
        return Math.Round(value, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatOneDecimal(double value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
