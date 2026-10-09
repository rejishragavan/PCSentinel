using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Monitors running OS processes to detect background tasks causing CPU clock spikes,
/// abnormal power surges, or thermal throttling events.
/// </summary>
public interface IProcessInspector
{
    /// <summary>
    /// Returns the top resource-consuming background processes sorted by CPU and memory load.
    /// </summary>
    Task<IReadOnlyList<ProcessActivity>> GetTopProcessesAsync(int limit = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes running processes to determine if a specific background process
    /// is responsible for an ongoing CPU frequency spike or thermal throttling state.
    /// </summary>
    Task<CulpritProcessSummary> DetectCulpritProcessesAsync(bool isThermalThrottling = false, CancellationToken cancellationToken = default);
}
