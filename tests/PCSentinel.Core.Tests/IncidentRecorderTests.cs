using PCSentinel.Core.Models;
using PCSentinel.Diagnostics;
using PCSentinel.Storage;
using Xunit;

namespace PCSentinel.Core.Tests;

public class IncidentRecorderTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly SqliteTelemetryStore _store;

    public IncidentRecorderTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"incident_test_{Guid.NewGuid():N}.db");
        _store = new SqliteTelemetryStore(_dbPath);
    }

    public async Task InitializeAsync()
    {
        await _store.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Fact]
    public async Task RingBuffer_RetainsLast30Samples_AndCapturesPreEventSnapshot()
    {
        var recorder = new IncidentRecorder(_store);

        // Feed 45 samples (1 to 45)
        for (int i = 1; i <= 45; i++)
        {
            recorder.IngestTelemetry(new TelemetrySample
            {
                CpuTemperatureC = 50.0 + i,
                CpuLoadPercent = i,
                GpuTemperatureC = 60.0 + i
            });
        }

        // Trigger incident
        var incident = await recorder.RecordIncidentAsync(
            triggerReason: "GPU Thermal Cutoff Triggered",
            severity: EventSeverity.Critical,
            healthScore: 72,
            correlatedEvents: new List<HealthEvent>
            {
                new() { Component = ComponentType.Gpu, Severity = EventSeverity.Critical, Type = HealthEventType.ThermalThrottling, Title = "GPU Hotspot > 105C" }
            });

        // Assert
        Assert.NotNull(incident);
        Assert.Equal(1, incident.IncidentNumber);
        Assert.Equal(30, incident.PreEventWindow.Count); // Capped at exactly 30 samples

        // First sample in buffer should be sample #16 (since 45 - 30 + 1 = 16)
        Assert.Equal(50.0 + 16, incident.PreEventWindow.First().CpuTemperatureC);
        Assert.Equal(50.0 + 45, incident.PreEventWindow.Last().CpuTemperatureC);

        // Verify retrieval from SQLite database
        var storedIncidents = await recorder.GetRecentIncidentsAsync(5);
        Assert.NotEmpty(storedIncidents);
        Assert.Equal("GPU Thermal Cutoff Triggered", storedIncidents.First().TriggerReason);
    }
}
