using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Maintains rolling high-frequency ring buffer and persists comprehensive
/// pre-event and post-event snapshots when critical incidents occur.
/// </summary>
public interface IIncidentRecorder
{
    void IngestTelemetry(TelemetrySample sample);
    Task<IncidentReport> RecordIncidentAsync(
        string triggerReason,
        EventSeverity severity,
        int healthScore,
        IReadOnlyList<HealthEvent> correlatedEvents,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentReport>> GetRecentIncidentsAsync(int count, CancellationToken cancellationToken = default);
}
