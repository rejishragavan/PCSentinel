namespace PCSentinel.Core.Models;

public enum WorkloadType
{
    Idle,
    Gaming,
    CpuHeavy,
    GpuHeavy,
    Rendering,
    MemoryHeavy,
    Custom
}

/// <summary>
/// Learned statistical baseline for a specific machine under a specific workload profile.
/// </summary>
public record WorkloadBaseline
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ProfileName { get; init; } = "Default";
    public WorkloadType Workload { get; init; } = WorkloadType.Idle;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public int SampleCount { get; init; }

    // Baseline ranges (mean and standard deviation)
    public double AvgCpuTempC { get; init; }
    public double StdDevCpuTempC { get; init; }
    public double AvgCpuClockMhz { get; init; }
    public double AvgCpuPowerW { get; init; }

    public double AvgGpuTempC { get; init; }
    public double StdDevGpuTempC { get; init; }
    public double AvgGpuClockMhz { get; init; }
    public double AvgGpuPowerW { get; init; }
}
