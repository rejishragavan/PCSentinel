using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Evaluates Windows boot transitions, compares with historical norms, and detects regressions.
/// </summary>
public interface IBootAnalyzer
{
    Task<BootSession?> AnalyzeLatestBootAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BootSession>> GetBootHistoryAsync(int count, CancellationToken cancellationToken = default);
    Task RecordBootSessionAsync(BootSession session, CancellationToken cancellationToken = default);
}
