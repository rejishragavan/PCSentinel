using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// OC Lab engine responsible for headroom estimation, baseline benchmarking,
/// and executing controlled stability experiments.
/// </summary>
public interface IOcAnalyzer
{
    OcHeadroomEstimate EstimateHeadroom(TelemetrySample currentSample, WorkloadBaseline? baseline = null);
    Task<BenchmarkResult> RunBenchmarkAsync(int durationSeconds = 15, double clockOffsetMhz = 0.0, CancellationToken cancellationToken = default);
    Task<OcExperiment> ExecuteControlledExperimentAsync(string label, double clockOffsetMhz, double voltageOffsetMv, CancellationToken cancellationToken = default);
    Task<AutoOcExecutionResult> ExecuteAutoTuneAsync(OcTarget target, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OcExperiment>> GetExperimentHistoryAsync(int count, CancellationToken cancellationToken = default);
}
