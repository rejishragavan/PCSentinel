using PCSentinel.Analysis;
using PCSentinel.Core.Models;
using PCSentinel.Storage;
using Xunit;

namespace PCSentinel.Core.Tests;

public class OcAnalyzerTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly SqliteTelemetryStore _store;

    public OcAnalyzerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"oc_test_{Guid.NewGuid():N}.db");
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
    public void CoolHardware_EstimatesHighHeadroom()
    {
        var analyzer = new OcAnalyzer(_store);
        var sample = new TelemetrySample
        {
            GpuTemperatureC = 67.5,
            GpuPowerWatts = 205.0,
            GpuClockMhz = 1845.0
        };

        var estimate = analyzer.EstimateHeadroom(sample);

        Assert.Equal(HeadroomLevel.High, estimate.ThermalHeadroom);
        Assert.Equal(HeadroomLevel.High, estimate.ClockHeadroom);
        Assert.True(estimate.RecommendedClockDeltaMhz >= 75.0);
        Assert.True(estimate.Confidence >= 0.80);
        Assert.Contains("+95 MHz", estimate.Rationale);
    }

    [Fact]
    public void OverheatingHardware_EstimatesZeroHeadroom()
    {
        var analyzer = new OcAnalyzer(_store);
        var sample = new TelemetrySample
        {
            GpuTemperatureC = 84.5,
            GpuPowerWatts = 280.0,
            GpuClockMhz = 1520.0
        };

        var estimate = analyzer.EstimateHeadroom(sample);

        Assert.Equal(HeadroomLevel.Low, estimate.ThermalHeadroom);
        Assert.Equal(0.0, estimate.RecommendedClockDeltaMhz);
        Assert.Contains("Minimal headroom", estimate.Rationale);
    }

    [Fact]
    public async Task RunControlledExperiment_CalculatesFpsGainAndLogsHistory()
    {
        var analyzer = new OcAnalyzer(_store);

        var exp = await analyzer.ExecuteControlledExperimentAsync(
            label: "Safe Boost Profile (+80MHz)",
            clockOffsetMhz: 80.0,
            voltageOffsetMv: 0.0);

        Assert.NotNull(exp);
        Assert.True(exp.StabilityPassed);
        Assert.True(exp.ExperimentFps > exp.BaselineFps);
        Assert.True(exp.PerformanceGainPercent > 0.0);

        var history = await analyzer.GetExperimentHistoryAsync(5);
        Assert.NotEmpty(history);
        Assert.Equal("Safe Boost Profile (+80MHz)", history.First().SettingLabel);
    }
}
