using PCSentinel.Analysis;
using PCSentinel.Core.Models;
using Xunit;

namespace PCSentinel.Core.Tests;

public class WorkloadClassifierTests
{
    private readonly WorkloadClassifier _classifier = new();

    [Fact]
    public void LowUtilization_ClassifiedAsIdle()
    {
        var sample = new TelemetrySample
        {
            CpuLoadPercent = 5.0,
            GpuLoadPercent = 2.0,
            RamLoadPercent = 35.0
        };

        var result = _classifier.Classify(sample);

        Assert.Equal(WorkloadType.Idle, result.Workload);
        Assert.True(result.Confidence >= 0.90);
    }

    [Fact]
    public void HighGpuAndModerateCpu_ClassifiedAsGaming()
    {
        var sample = new TelemetrySample
        {
            CpuLoadPercent = 42.0,
            GpuLoadPercent = 95.0,
            RamLoadPercent = 60.0
        };

        var result = _classifier.Classify(sample);

        Assert.Equal(WorkloadType.Gaming, result.Workload);
        Assert.True(result.Confidence >= 0.85);
    }

    [Fact]
    public void HighCpuAndLowGpu_ClassifiedAsCpuHeavy()
    {
        var sample = new TelemetrySample
        {
            CpuLoadPercent = 92.0,
            GpuLoadPercent = 8.0,
            RamLoadPercent = 50.0
        };

        var result = _classifier.Classify(sample);

        Assert.Equal(WorkloadType.CpuHeavy, result.Workload);
        Assert.True(result.Confidence >= 0.90);
    }
}
