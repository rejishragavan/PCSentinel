using PCSentinel.Diagnostics;
using Xunit;

namespace PCSentinel.Core.Tests;

public class ProcessInspectorTests
{
    [Fact]
    public async Task DetectCulpritProcesses_WhenThrottling_IdentifiesHighCpuCulprit()
    {
        var inspector = new ProcessInspector(simulateCulprits: true);

        var result = await inspector.DetectCulpritProcessesAsync(isThermalThrottling: true);

        Assert.True(result.HasCulprit);
        Assert.NotNull(result.PrimaryCulprit);
        Assert.True(result.PrimaryCulprit.CpuPercent >= 50.0);
        Assert.True(result.PrimaryCulprit.IsSuspect);
        Assert.Contains("ShaderCompileWorker", result.PrimaryCulprit.ProcessName);
        Assert.Contains("Culprit task detected", result.TechnicalDiagnosis);
    }

    [Fact]
    public async Task DetectCulpritProcesses_WhenNominal_ReportsNoCulprits()
    {
        var inspector = new ProcessInspector(simulateCulprits: true);

        var result = await inspector.DetectCulpritProcessesAsync(isThermalThrottling: false);

        Assert.False(result.HasCulprit);
        Assert.Null(result.PrimaryCulprit);
        Assert.Contains("balanced", result.TechnicalDiagnosis);
    }

    [Fact]
    public async Task GetTopProcesses_ReturnsNonEmptyList()
    {
        var inspector = new ProcessInspector(simulateCulprits: false);

        var processes = await inspector.GetTopProcessesAsync(limit: 5);

        Assert.NotNull(processes);
        Assert.NotEmpty(processes);
        Assert.True(processes.Count <= 5);
        foreach (var p in processes)
        {
            Assert.False(string.IsNullOrEmpty(p.ProcessName));
        }
    }
}
