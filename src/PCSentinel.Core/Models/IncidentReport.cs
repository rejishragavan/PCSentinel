namespace PCSentinel.Core.Models;

/// <summary>
/// Comprehensive "Black Box" incident recorder snapshot.
/// Captures high-frequency pre-event telemetry (10-30s), active events,
/// driver states, and post-event recovery state around a critical crash/throttling event.
/// </summary>
public record IncidentReport
{
    public long IncidentNumber { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string TriggerReason { get; init; } = string.Empty;
    public EventSeverity Severity { get; init; } = EventSeverity.Critical;
    public int HealthScoreAtTrigger { get; init; }

    // Pre-incident and post-incident telemetry time-series windows
    public IReadOnlyList<TelemetrySample> PreEventWindow { get; init; } = Array.Empty<TelemetrySample>();
    public IReadOnlyList<TelemetrySample> PostEventWindow { get; init; } = Array.Empty<TelemetrySample>();

    // Correlated environmental signals
    public IReadOnlyList<HealthEvent> CorrelatedEvents { get; init; } = Array.Empty<HealthEvent>();
    public IReadOnlyList<DriverInfo> DegradedDrivers { get; init; } = Array.Empty<DriverInfo>();
    public IReadOnlyList<string> SystemLogs { get; init; } = Array.Empty<string>();

    public string Summary =>
        $"Incident #{IncidentNumber:D5} [{Severity}] at {Timestamp:HH:mm:ss}: {TriggerReason} (Pre-samples: {PreEventWindow.Count})";
}
