using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

public record AnalysisResult(HealthScore Score, IReadOnlyList<HealthEvent> DetectedEvents);

/// <summary>
/// Engine that evaluates telemetry against deterministic rules and personal baselines.
/// </summary>
public interface IHealthAnalyzer
{
    AnalysisResult Analyze(TelemetrySample current, WorkloadBaseline? baseline = null);
}

/// <summary>
/// Provider for system, driver, or storage diagnostics.
/// </summary>
public interface IDiagnosticProvider
{
    string DiagnosticDomain { get; }
    Task<IReadOnlyList<HealthEvent>> InspectAsync(CancellationToken cancellationToken = default);
}
