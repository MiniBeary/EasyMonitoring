namespace EasyMonitoring.Core.Sensors;

public sealed record HardwareReadings(
    string HardwareType,
    string Name,
    IReadOnlyList<SensorReading> Sensors);
