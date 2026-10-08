namespace PCSentinel.Core.Models;

/// <summary>
/// Aggregated snapshot of PC telemetry at a point in time.
/// </summary>
public record TelemetrySample
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // CPU Metrics
    public double? CpuTemperatureC { get; init; }
    public double? CpuClockMhz { get; init; }
    public double? CpuLoadPercent { get; init; }
    public double? CpuPowerWatts { get; init; }

    // GPU Metrics
    public double? GpuTemperatureC { get; init; }
    public double? GpuHotspotC { get; init; }
    public double? GpuClockMhz { get; init; }
    public double? GpuLoadPercent { get; init; }
    public double? GpuPowerWatts { get; init; }
    public double? GpuFanPercent { get; init; }

    // RAM Metrics
    public double? RamUsedGb { get; init; }
    public double? RamTotalGb { get; init; }
    public double? RamLoadPercent { get; init; }

    // Storage Metrics (Drive name -> Temperature)
    public Dictionary<string, double> StorageTemperaturesC { get; init; } = new();

    // Raw readings included in this sample
    public IReadOnlyList<SensorReading> RawReadings { get; init; } = Array.Empty<SensorReading>();
}
