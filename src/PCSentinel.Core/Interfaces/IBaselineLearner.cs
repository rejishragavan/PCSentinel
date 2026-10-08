using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Maintains rolling statistics per workload profile using streaming algorithms.
/// </summary>
public interface IBaselineLearner
{
    void IngestSample(TelemetrySample sample, WorkloadType workload);
    WorkloadBaseline? GetBaseline(WorkloadType workload);
    IReadOnlyCollection<WorkloadBaseline> GetAllBaselines();
    void Reset(WorkloadType? workload = null);
}
