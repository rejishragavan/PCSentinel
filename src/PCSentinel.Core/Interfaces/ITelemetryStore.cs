using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Abstraction for persisting and querying telemetry, health events, and baselines.
/// </summary>
public interface ITelemetryStore : IAsyncDisposable
{
    Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);
    Task StoreTelemetrySampleAsync(TelemetrySample sample, CancellationToken cancellationToken = default);
    Task StoreHealthEventAsync(HealthEvent healthEvent, CancellationToken cancellationToken = default);
    Task StoreHealthScoreAsync(HealthScore score, CancellationToken cancellationToken = default);
    Task StoreBaselineAsync(WorkloadBaseline baseline, CancellationToken cancellationToken = default);

    Task StoreBootSessionAsync(BootSession session, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BootSession>> GetBootSessionsAsync(int count, CancellationToken cancellationToken = default);

    Task StoreIncidentAsync(IncidentReport incident, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentReport>> GetIncidentsAsync(int count, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TelemetrySample>> GetRecentSamplesAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthEvent>> GetRecentEventsAsync(int count, CancellationToken cancellationToken = default);
    Task<HealthScore?> GetLatestHealthScoreAsync(CancellationToken cancellationToken = default);
    Task<WorkloadBaseline?> GetBaselineAsync(WorkloadType workload, CancellationToken cancellationToken = default);
}
