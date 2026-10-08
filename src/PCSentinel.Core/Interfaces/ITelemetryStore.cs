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

    Task<IReadOnlyList<TelemetrySample>> GetRecentSamplesAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HealthEvent>> GetRecentEventsAsync(int count, CancellationToken cancellationToken = default);
    Task<HealthScore?> GetLatestHealthScoreAsync(CancellationToken cancellationToken = default);
    Task<WorkloadBaseline?> GetBaselineAsync(WorkloadType workload, CancellationToken cancellationToken = default);
}
