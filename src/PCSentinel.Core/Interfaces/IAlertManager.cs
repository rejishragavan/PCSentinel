using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Centralized alert dispatcher for routing diagnostic events to the UI, persistent logs,
/// and external hardware sentinel nodes.
/// </summary>
public interface IAlertManager
{
    event EventHandler<HealthEvent>? AlertPublished;
    Task PublishAlertAsync(HealthEvent healthEvent, CancellationToken cancellationToken = default);
    IReadOnlyList<HealthEvent> GetActiveAlerts();
    void AcknowledgeAlert(Guid alertId);
}
