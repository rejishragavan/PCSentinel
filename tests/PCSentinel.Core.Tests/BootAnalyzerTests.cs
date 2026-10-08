using PCSentinel.Core.Models;
using PCSentinel.Diagnostics;
using PCSentinel.Storage;
using Xunit;

namespace PCSentinel.Core.Tests;

public class BootAnalyzerTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly SqliteTelemetryStore _store;

    public BootAnalyzerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"boot_test_{Guid.NewGuid():N}.db");
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
    public async Task NormalBoot_IsNotFlaggedAsRegression()
    {
        var analyzer = new BootAnalyzer(_store, simulateRegression: false);

        var session = await analyzer.AnalyzeLatestBootAsync();

        Assert.NotNull(session);
        Assert.False(session.IsRegression);
        Assert.InRange(session.TotalBootDurationMs, 14000, 16000);
    }

    [Fact]
    public async Task SlowBoot_IsFlaggedAsRegression_WithDriverExplanation()
    {
        // First record a healthy historical baseline boot (15.5s)
        await _store.StoreBootSessionAsync(new BootSession
        {
            BootId = 1,
            TotalBootDurationMs = 15500,
            MainPathBootDurationMs = 9000,
            DriverInitDurationMs = 3000,
            PostBootDurationMs = 5500,
            IsRegression = false
        });

        var analyzer = new BootAnalyzer(_store, simulateRegression: true);
        var session = await analyzer.AnalyzeLatestBootAsync();

        Assert.NotNull(session);
        Assert.True(session.IsRegression);
        Assert.Equal("Device Drivers", session.SlowestSubsystem);
        Assert.Contains("Driver initialization", session.RegressionExplanation);
    }
}
