using PCSentinel.Diagnostics;
using Xunit;

namespace PCSentinel.Core.Tests;

public class CrashAnalyzerTests
{
    [Fact]
    public void SimulateCrash_VideoTdrBsod_ProducesExpectedBugcheckAndDriver()
    {
        var analyzer = new CrashAnalyzer();
        var report = analyzer.SimulateCrash(CrashSimulationType.VideoTdrBsod);

        Assert.True(report.HasCrash);
        Assert.Equal("0x00000116", report.BugcheckCode);
        Assert.Equal("VIDEO_TDR_FAILURE", report.BugcheckName);
        Assert.Equal("nvlddmkm.sys", report.OffendingDriver);
        Assert.Contains("TDR watchdog threshold", report.RootCauseAnalysis);
        Assert.NotEmpty(report.RecommendedMitigations);
        Assert.Contains(report.RecommendedMitigations, m => m.Contains("overclock offset"));
    }

    [Fact]
    public void SimulateCrash_GpuDriverTdr_DetectsEvent4101Recovery()
    {
        var analyzer = new CrashAnalyzer();
        var report = analyzer.SimulateCrash(CrashSimulationType.GpuDriverTdr);

        Assert.True(report.HasCrash);
        Assert.Equal("EVENT_4101", report.BugcheckCode);
        Assert.Equal("DISPLAY_DRIVER_STOPPED_RESPONDING", report.BugcheckName);
        Assert.Equal("nvlddmkm.sys", report.OffendingDriver);
        Assert.Contains("recovered by Windows Desktop Window Manager", report.RootCauseAnalysis);
        Assert.NotEmpty(report.RecommendedMitigations);
    }

    [Fact]
    public void SimulateCrash_PowerLossShutdown_IdentifiesKernelPower41()
    {
        var analyzer = new CrashAnalyzer();
        var report = analyzer.SimulateCrash(CrashSimulationType.PowerLossDirtyShutdown);

        Assert.True(report.HasCrash);
        Assert.Equal("EVENT_41_CAT_63", report.BugcheckCode);
        Assert.Equal("KERNEL_POWER_DIRTY_SHUTDOWN", report.BugcheckName);
        Assert.Contains("ntoskrnl.exe", report.OffendingDriver);
        Assert.Contains("Event ID 41", report.RootCauseAnalysis);
        Assert.NotEmpty(report.RecommendedMitigations);
    }
}
