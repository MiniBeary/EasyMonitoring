namespace EasyMonitoring.Core.Sensors;

public sealed record SensorReading(string Name, SensorKind Kind, double? Value);
