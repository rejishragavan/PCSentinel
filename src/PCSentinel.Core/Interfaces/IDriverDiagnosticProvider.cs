using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Probes device manager and PnP status for degraded, stopped, or missing hardware drivers.
/// </summary>
public interface IDriverDiagnosticProvider
{
    Task<DriverHealthSummary> ScanDriversAsync(CancellationToken cancellationToken = default);
}
