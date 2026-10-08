using System.Collections.Concurrent;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Analysis;

/// <summary>
/// Thread-safe alert dispatcher and deduplicator.
/// Routes events to subscribers (UI, persistent database, and external USB Sentinel Node).
/// </summary>
public sealed class AlertManager : IAlertManager
{
    private readonly ConcurrentDictionary<string, HealthEvent> _activeAlerts = new();
    private readonly ITelemetryStore? _store;
    private readonly ISentinelNode? _sentinelNode;

    public event EventHandler<HealthEvent>? AlertPublished;

    public AlertManager(ITelemetryStore? store = null, ISentinelNode? sentinelNode = null)
    {
        _store = store;
        _sentinelNode = sentinelNode;
    }

    public async Task PublishAlertAsync(HealthEvent healthEvent, CancellationToken cancellationToken = default)
    {
        var key = $"{healthEvent.Component}_{healthEvent.Type}";

        // Deduplicate within active window
        bool isNew = _activeAlerts.TryAdd(key, healthEvent);
        if (!isNew)
        {
            _activeAlerts[key] = healthEvent; // Update evidence
        }

        // Notify subscribers
        AlertPublished?.Invoke(this, healthEvent);

        // Store to database if available
        if (_store != null)
        {
            try
            {
                await _store.StoreHealthEventAsync(healthEvent, cancellationToken);
            }
            catch { }
        }

        // Forward to USB Sentinel Node if available
        if (_sentinelNode != null && _sentinelNode.IsConnected)
        {
            try
            {
                var packet = new SentinelPacket
                {
                    Type = "alert",
                    LedColor = healthEvent.Severity == EventSeverity.Critical ? "RED" : "YELLOW",
                    AlertTitle = healthEvent.Title,
                    TriggerBuzzer = healthEvent.Severity == EventSeverity.Critical
                };
                await _sentinelNode.SendTelemetryAsync(packet, cancellationToken);
            }
            catch { }
        }
    }

    public IReadOnlyList<HealthEvent> GetActiveAlerts() => _activeAlerts.Values.ToList();

    public void AcknowledgeAlert(Guid alertId)
    {
        var match = _activeAlerts.FirstOrDefault(kvp => kvp.Value.Id == alertId);
        if (!string.IsNullOrEmpty(match.Key))
        {
            _activeAlerts.TryRemove(match.Key, out _);
        }
    }
}
