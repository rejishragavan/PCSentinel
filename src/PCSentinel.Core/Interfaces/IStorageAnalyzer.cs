using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Evaluates storage health, SMART attributes, and long-term flash/magnetic degradation.
/// </summary>
public interface IStorageAnalyzer
{
    Task<IReadOnlyList<StorageDriveInfo>> InspectStorageDrivesAsync(CancellationToken cancellationToken = default);
}
