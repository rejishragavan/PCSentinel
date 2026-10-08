namespace PCSentinel.Core.Models;

/// <summary>
/// Specific types of recovery and remediation actions supported by PC Sentinel (Phase 19).
/// </summary>
public enum RecoveryActionType
{
    None = 0,
    SystemFileCheckSfc = 1,
    DismHealthRestore = 2,
    CheckDiskScan = 3,
    DriverRollback = 4,
    SafeModeReboot = 5,
    RestorePointRollback = 6,
    ThermalCooldownWait = 7,
    BootBcdRepair = 8
}

/// <summary>
/// Specific root-cause issue detected that requires system repair.
/// </summary>
public sealed class RecoveryIssue
{
    public string IssueCode { get; init; } = string.Empty;
    public EventSeverity Severity { get; init; } = EventSeverity.Warning;
    public string Description { get; init; } = string.Empty;
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
    public RecoveryActionType SuggestedAction { get; init; } = RecoveryActionType.None;
    public string TechnicalEvidence { get; init; } = string.Empty;
}

/// <summary>
/// Result of an individual automated recovery or diagnostic command.
/// </summary>
public sealed class RecoveryTaskResult
{
    public string TaskId { get; init; } = Guid.NewGuid().ToString("N");
    public RecoveryActionType Action { get; init; }
    public string CommandExecuted { get; init; } = string.Empty;
    public bool IsSuccessful { get; init; }
    public int ExitCode { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string OutputLog { get; init; } = string.Empty;
    public double DurationMs { get; init; }
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Holistic assessment of whether the operating system requires autonomous recovery.
/// </summary>
public sealed class RecoveryAssessment
{
    public bool SystemRequiresRecovery { get; init; }
    public EventSeverity OverallSeverity { get; init; } = EventSeverity.Info;
    public string PrimaryRootCause { get; init; } = "System operating within nominal recovery parameters.";
    public List<RecoveryIssue> DetectedIssues { get; init; } = new();
    public List<RecoveryActionType> RecommendedActions { get; init; } = new();
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Orchestrated recovery plan executing multi-step repair workflows.
/// </summary>
public sealed class RecoveryPlan
{
    public string PlanId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public string TriggerReason { get; init; } = string.Empty;
    public List<RecoveryTaskResult> ExecutedTasks { get; init; } = new();
    public bool IsCompleted { get; set; }
    public string ResolutionSummary { get; set; } = string.Empty;
    public int PostRecoveryHealthScore { get; set; } = 100;
}
