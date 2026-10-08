namespace PCSentinel.Core.Models;

/// <summary>
/// Represents a normalized single sensor reading from any hardware provider.
/// </summary>
public record SensorReading
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public ComponentType Component { get; init; }
    public string ComponentName { get; init; } = string.Empty;
    public SensorType SensorType { get; init; }
    public string SensorName { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public bool IsAvailable { get; init; } = true;
    public string Source { get; init; } = "LibreHardwareMonitor";

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss}] {Component} ({ComponentName}) - {SensorName}: {Value:F1} {Unit}";
}
