namespace PCSentinel.Core.Models;

/// <summary>
/// Result of evaluating real-time telemetry to determine active system state.
/// </summary>
public record WorkloadClassification
{
    public WorkloadType Workload { get; init; } = WorkloadType.Idle;
    public double Confidence { get; init; } = 1.0;
    public string Description { get; init; } = "Idle State";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
