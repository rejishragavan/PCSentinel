using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Diagnostics;

/// <summary>
/// Phase 14 Black Box Incident Recorder.
/// Maintains rolling 30-second pre-event telemetry buffer. Upon critical events,
/// freezes and packages pre/post telemetry, active environmental diagnostics,
/// and degraded driver states for post-mortem root-cause analysis.
/// </summary>
public sealed class IncidentRecorder : IIncidentRecorder
{
    private readonly ITelemetryStore _store;
    private readonly IDriverDiagnosticProvider? _driverProvider;
    private readonly Queue<TelemetrySample> _ringBuffer = new();
    private readonly object _lock = new();
    private const int PreWindowCapacity = 30; // 30 samples = 30 seconds at 1Hz
    private long _incidentSequence = 0;

    public event EventHandler<IncidentReport>? IncidentLogged;

    public IncidentRecorder(ITelemetryStore store, IDriverDiagnosticProvider? driverProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _driverProvider = driverProvider;
    }

    public void IngestTelemetry(TelemetrySample sample)
    {
        lock (_lock)
        {
            if (_ringBuffer.Count >= PreWindowCapacity)
            {
                _ringBuffer.Dequeue();
            }
            _ringBuffer.Enqueue(sample);
        }
    }

    public async Task<IncidentReport> RecordIncidentAsync(
        string triggerReason,
        EventSeverity severity,
        int healthScore,
        IReadOnlyList<HealthEvent> correlatedEvents,
        CancellationToken cancellationToken = default)
    {
        List<TelemetrySample> preWindow;
        lock (_lock)
        {
            preWindow = _ringBuffer.ToList();
        }

        // Check for degraded drivers at moment of incident
        var degradedDrivers = new List<DriverInfo>();
        if (_driverProvider != null)
        {
            try
            {
                var summary = await _driverProvider.ScanDriversAsync(cancellationToken);
                degradedDrivers.AddRange(summary.Devices.Where(d => d.Status != DeviceStatusCode.Ok));
            }
            catch { }
        }

        Interlocked.Increment(ref _incidentSequence);

        var report = new IncidentReport
        {
            IncidentNumber = _incidentSequence,
            Timestamp = DateTimeOffset.UtcNow,
            TriggerReason = triggerReason,
            Severity = severity,
            HealthScoreAtTrigger = healthScore,
            PreEventWindow = preWindow,
            PostEventWindow = Array.Empty<TelemetrySample>(), // populated during active observation
            CorrelatedEvents = correlatedEvents,
            DegradedDrivers = degradedDrivers,
            SystemLogs = new[] { $"Triggered: {triggerReason}", $"Health Score: {healthScore}/100" }
        };

        // Persist Black Box Incident in SQLite
        await _store.StoreIncidentAsync(report, cancellationToken);

        IncidentLogged?.Invoke(this, report);
        return report;
    }

    public Task<IReadOnlyList<IncidentReport>> GetRecentIncidentsAsync(int count, CancellationToken cancellationToken = default)
    {
        return _store.GetIncidentsAsync(count, cancellationToken);
    }
}
