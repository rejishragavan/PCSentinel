using PCSentinel.Diagnostics;
using Xunit;

namespace PCSentinel.Core.Tests;

public class StorageAnalyzerTests
{
    [Fact]
    public async Task HealthyDrive_ReportsHighHealthAndLowRisk()
    {
        var analyzer = new StorageAnalyzer(simulateDegraded: false);
        var drives = await analyzer.InspectStorageDrivesAsync();

        Assert.NotEmpty(drives);
        var drive = drives.First();

        Assert.Contains("Samsung SSD 990 PRO", drive.Model);
        Assert.Equal(99, drive.HealthPercentage);
        Assert.Equal(0, drive.ReallocatedSectors);
        Assert.Contains("Nominal", drive.DegradationRisk);
        Assert.NotEmpty(drive.SmartAttributes);
    }

    [Fact]
    public async Task DegradedDrive_DetectsReallocatedSectorsAndModerateRisk()
    {
        var analyzer = new StorageAnalyzer(simulateDegraded: true);
        var drives = await analyzer.InspectStorageDrivesAsync();

        Assert.NotEmpty(drives);
        var drive = drives.First();

        Assert.True(drive.HealthPercentage < 80);
        Assert.True(drive.ReallocatedSectors > 0);
        Assert.Contains("Moderate Risk", drive.DegradationRisk);
    }
}
