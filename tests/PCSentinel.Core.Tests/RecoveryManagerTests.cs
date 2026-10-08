using PCSentinel.Core.Models;
using PCSentinel.Diagnostics;
using PCSentinel.Storage;
using Xunit;

namespace PCSentinel.Core.Tests;

public class RecoveryManagerTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly SqliteTelemetryStore _store;

    public RecoveryManagerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"recovery_test_{Guid.NewGuid():N}.db");
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
    public async Task HealthySystem_RequiresNoRecovery()
    {
        var driverInspector = new PnpDeviceInspector(simulateDegraded: false);
        var bootAnalyzer = new BootAnalyzer(_store, simulateRegression: false);
        var manager = new RecoveryManager(_store, driverInspector, bootAnalyzer, simulateProblems: false);

        var assessment = await manager.AssessSystemRecoveryStateAsync();

        Assert.False(assessment.SystemRequiresRecovery);
        Assert.Empty(assessment.DetectedIssues);
        Assert.Contains("nominal", assessment.PrimaryRootCause.ToLower());
    }

    [Fact]
    public async Task DegradedDriver_TriggersDriverRollbackAction()
    {
        var driverInspector = new PnpDeviceInspector(simulateDegraded: true);
        var bootAnalyzer = new BootAnalyzer(_store, simulateRegression: false);
        var manager = new RecoveryManager(_store, driverInspector, bootAnalyzer, simulateProblems: false);

        var assessment = await manager.AssessSystemRecoveryStateAsync();

        Assert.True(assessment.SystemRequiresRecovery);
        Assert.Contains(RecoveryActionType.DriverRollback, assessment.RecommendedActions);
        Assert.Contains(assessment.DetectedIssues, i => i.SuggestedAction == RecoveryActionType.DriverRollback);
    }

    [Fact]
    public async Task OrchestrateFullRecoveryPlan_ExecutesAllTasksAndLogsAudits()
    {
        var driverInspector = new PnpDeviceInspector(simulateDegraded: false);
        var bootAnalyzer = new BootAnalyzer(_store, simulateRegression: false);
        var manager = new RecoveryManager(_store, driverInspector, bootAnalyzer, simulateProblems: true);

        var plan = await manager.OrchestrateFullRecoveryPlanAsync(dryRun: true);

        Assert.NotNull(plan);
        Assert.True(plan.IsCompleted);
        Assert.NotEmpty(plan.ExecutedTasks);
        Assert.All(plan.ExecutedTasks, t => Assert.True(t.IsSuccessful));
        Assert.True(plan.PostRecoveryHealthScore >= 90);

        var auditHistory = await manager.GetRecoveryAuditHistoryAsync(10);
        Assert.NotEmpty(auditHistory);
        Assert.Equal(plan.ExecutedTasks.Count, auditHistory.Count);
    }
}
