using PCSentinel.Analysis;
using PCSentinel.Core.Models;
using Xunit;

namespace PCSentinel.Core.Tests;

public class MultiSignalCorrelationTests
{
    private readonly MultiSignalCorrelationEngine _engine = new();

    [Fact]
    public void HighTempAndDownclockedGpu_DiagnosesThermalThrottling()
    {
        var sample = new TelemetrySample
        {
            GpuLoadPercent = 98.0,
            GpuTemperatureC = 86.0,
            GpuHotspotC = 101.0,
            GpuClockMhz = 1410.0,
            GpuFanPercent = 100.0
        };

        var result = _engine.Analyze(sample);

        Assert.NotEmpty(result.DetectedEvents);
        var alert = Assert.Single(result.DetectedEvents, e => e.Type == HealthEventType.ThermalThrottling);
        Assert.Equal(ComponentType.Gpu, alert.Component);
        Assert.True(alert.Confidence >= 0.90);
        Assert.True(result.Score.GpuScore < 90);
    }

    [Fact]
    public void CoolTempAndLowClockAndLowPowerUnderGaming_DiagnosesPowerLimit()
    {
        var sample = new TelemetrySample
        {
            CpuLoadPercent = 35.0,
            GpuLoadPercent = 95.0,
            GpuTemperatureC = 64.0, // cool
            GpuClockMhz = 1420.0,   // downclocked
            GpuPowerWatts = 145.0   // low power
        };

        var result = _engine.Analyze(sample);

        Assert.Contains(result.DetectedEvents, e => e.Type == HealthEventType.PowerThrottling);
    }

    [Fact]
    public void BaselineComparison_DetectsCoolingDegradation()
    {
        var baseline = new WorkloadBaseline
        {
            ProfileName = "Learned Profile (Gaming)",
            Workload = WorkloadType.Gaming,
            AvgGpuTempC = 70.0,
            StdDevGpuTempC = 2.0,
            AvgGpuClockMhz = 1845.0
        };

        // Running 12C hotter than baseline with lower clock
        var sample = new TelemetrySample
        {
            CpuLoadPercent = 35.0,
            GpuLoadPercent = 95.0,
            GpuTemperatureC = 82.0,
            GpuClockMhz = 1620.0
        };

        var result = _engine.Analyze(sample, baseline);

        Assert.Contains(result.DetectedEvents, e => e.Type == HealthEventType.CoolingDegradation);
    }
}
