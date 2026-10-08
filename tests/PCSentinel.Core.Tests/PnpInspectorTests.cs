using PCSentinel.Core.Models;
using PCSentinel.Diagnostics;
using Xunit;

namespace PCSentinel.Core.Tests;

public class PnpInspectorTests
{
    [Fact]
    public async Task HealthyDriverScan_ReturnsZeroErrors()
    {
        var inspector = new PnpDeviceInspector(simulateDegraded: false);

        var report = await inspector.ScanDriversAsync();

        Assert.NotNull(report);
        Assert.Equal(0, report.ErrorCount);
        Assert.False(report.HasCriticalFailure);
    }

    [Fact]
    public async Task InjectedGpuDriverFailure_IdentifiesCode43()
    {
        var inspector = new PnpDeviceInspector(simulateDegraded: true);

        var report = await inspector.ScanDriversAsync();

        Assert.NotNull(report);
        Assert.True(report.HasCriticalFailure);
        Assert.Equal(1, report.ErrorCount);

        var gpu = Assert.Single(report.Devices, d => d.Category == "GPU");
        Assert.Equal(DeviceStatusCode.Error, gpu.Status);
        Assert.Equal(43, gpu.ConfigManagerErrorCode);
        Assert.Contains("Code 43", gpu.ErrorDescription);
    }
}
