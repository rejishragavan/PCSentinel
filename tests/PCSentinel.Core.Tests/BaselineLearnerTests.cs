using PCSentinel.Analysis;
using PCSentinel.Core.Models;
using Xunit;

namespace PCSentinel.Core.Tests;

public class BaselineLearnerTests
{
    [Fact]
    public void IngestSamples_ComputesCorrectMeanAndStandardDeviation()
    {
        var learner = new StatisticalBaselineLearner();

        // Feed 10 samples with known GPU temps: 70, 72, 71, 73, 70, 72, 71, 74, 70, 71
        // Mean = 71.4
        double[] temps = { 70.0, 72.0, 71.0, 73.0, 70.0, 72.0, 71.0, 74.0, 70.0, 71.0 };

        foreach (var t in temps)
        {
            learner.IngestSample(new TelemetrySample
            {
                CpuTemperatureC = 55.0,
                CpuClockMhz = 4500.0,
                GpuTemperatureC = t,
                GpuClockMhz = 1845.0,
                GpuPowerWatts = 200.0
            }, WorkloadType.Gaming);
        }

        var baseline = learner.GetBaseline(WorkloadType.Gaming);

        Assert.NotNull(baseline);
        Assert.Equal(10, baseline.SampleCount);
        Assert.InRange(baseline.AvgGpuTempC, 71.39, 71.41);
        Assert.True(baseline.StdDevGpuTempC > 0.0);
        Assert.Equal(1845.0, baseline.AvgGpuClockMhz);
    }
}
