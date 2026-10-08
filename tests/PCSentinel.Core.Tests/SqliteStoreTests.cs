using PCSentinel.Core.Models;
using PCSentinel.Storage;
using Xunit;

namespace PCSentinel.Core.Tests;

public class SqliteStoreTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly SqliteTelemetryStore _store;

    public SqliteStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sentinel_test_{Guid.NewGuid():N}.db");
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
    public async Task StoreAndRetrieveTelemetrySample_Succeeds()
    {
        // Arrange
        var sample = new TelemetrySample
        {
            CpuTemperatureC = 62.5,
            CpuClockMhz = 4800,
            CpuLoadPercent = 45.2,
            GpuTemperatureC = 71.0,
            GpuClockMhz = 1845,
            RamUsedGb = 14.2
        };

        // Act
        await _store.StoreTelemetrySampleAsync(sample);
        var recent = await _store.GetRecentSamplesAsync(5);

        // Assert
        Assert.NotEmpty(recent);
        var stored = recent.First();
        Assert.Equal(62.5, stored.CpuTemperatureC);
        Assert.Equal(71.0, stored.GpuTemperatureC);
        Assert.Equal(14.2, stored.RamUsedGb);
    }

    [Fact]
    public async Task StoreAndRetrieveHealthEvent_Succeeds()
    {
        // Arrange
        var healthEvent = new HealthEvent
        {
            Component = ComponentType.Gpu,
            Severity = EventSeverity.Warning,
            Type = HealthEventType.ThermalThrottling,
            Title = "GPU Thermal Warning",
            Evidence = "GPU exceeded 85C",
            Confidence = 0.95,
            Recommendation = "Check fan profile"
        };

        // Act
        await _store.StoreHealthEventAsync(healthEvent);
        var recent = await _store.GetRecentEventsAsync(5);

        // Assert
        Assert.NotEmpty(recent);
        var stored = recent.First();
        Assert.Equal("GPU Thermal Warning", stored.Title);
        Assert.Equal(EventSeverity.Warning, stored.Severity);
    }
}
