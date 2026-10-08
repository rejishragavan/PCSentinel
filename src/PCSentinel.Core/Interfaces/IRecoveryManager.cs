using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Autonomous recovery orchestration engine (Phase 19).
/// Evaluates system failure states, diagnoses corrupted filesystems/drivers,
/// and orchestrates repair workflows.
/// </summary>
public interface IRecoveryManager
{
    /// <summary>
    /// Analyzes the system state (dirty shutdowns, crash incidents, driver errors, boot regressions)
    /// to determine if remediation or repair is necessary.
    /// </summary>
    Task<RecoveryAssessment> AssessSystemRecoveryStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an individual recovery task (e.g. SFC scannow, DISM repair, CheckDisk).
    /// </summary>
    Task<RecoveryTaskResult> ExecuteRecoveryActionAsync(
        RecoveryActionType action,
        bool dryRun = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates an end-to-end recovery plan addressing all detected root causes.
    /// </summary>
    Task<RecoveryPlan> OrchestrateFullRecoveryPlanAsync(
        bool dryRun = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves historical recovery audit records from persistence.
    /// </summary>
    Task<IReadOnlyList<RecoveryTaskResult>> GetRecoveryAuditHistoryAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);
}
