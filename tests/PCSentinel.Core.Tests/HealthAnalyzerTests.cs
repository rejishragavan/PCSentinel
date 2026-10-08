using PCSentinel.Analysis;
using PCSentinel.Core.Models;
using Xunit;

namespace PCSentinel.Core.Tests;

public class HealthAnalyzerTests
{
    private readonly RuleBasedHealthAnalyzer _analyzer = new();

    [Fact]
    public void NormalWorkload_ReturnsHighScore_AndNoCriticalEvents()
    {
        // Arrange
        var sample = new TelemetrySample
        {
            CpuTemperatureC = 58.0,
            CpuClockMhz = 4700.0,
            CpuLoadPercent = 40.0,
            GpuTemperatureC = 68.0,
            GpuClockMhz = 1845.0,
            GpuLoadPercent = 75.0,
            RamLoadPercent = 45.0
        };

        // Act
        var result = _analyzer.Analyze(sample);

        // Assert
        Assert.True(result.Score.OverallScore >= 95);
        Assert.Empty(result.DetectedEvents);
        Assert.Empty(result.Score.Deductions);
    }

    [Fact]
    public void OverheatingGpu_TriggersThermalThrottling_WithExplainableDeduction()
    {
        // Arrange
        var sample = new TelemetrySample
        {
            GpuTemperatureC = 87.0,
            GpuHotspotC = 103.0,
            GpuLoadPercent = 99.0,
            GpuClockMhz = 1450.0
        };

        // Act
        var result = _analyzer.Analyze(sample);

        // Assert
        Assert.NotEmpty(result.DetectedEvents);
        var throttleEvent = Assert.Single(result.DetectedEvents, e => e.Type == HealthEventType.ThermalThrottling);
        Assert.Equal(ComponentType.Gpu, throttleEvent.Component);
        Assert.True(throttleEvent.Confidence >= 0.90);
        Assert.Contains("GPU Core: 87.0°C", throttleEvent.Evidence);
        Assert.True(result.Score.GpuScore < 90);
    }

    [Fact]
    public void BaselineDeviation_DetectsDegradedPerformance()
    {
        // Arrange: Baseline was 72°C @ 1845 MHz
        var baseline = new WorkloadBaseline
        {
            ProfileName = "Normal Gaming",
            Workload = WorkloadType.Gaming,
            AvgGpuTempC = 72.0,
            AvgGpuClockMhz = 1845.0
        };

        // Current is running hotter (+11°C) with lower clock (-245 MHz = -13.2%)
        var sample = new TelemetrySample
        {
            GpuTemperatureC = 83.0,
            GpuClockMhz = 1600.0,
            GpuLoadPercent = 95.0
        };

        // Act
        var result = _analyzer.Analyze(sample, baseline);

        // Assert
        Assert.Contains(result.DetectedEvents, e => e.Type == HealthEventType.ThermalPerformanceAnomaly);
    }
}
