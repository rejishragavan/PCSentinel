namespace PCSentinel.Core.Models;

/// <summary>
/// Snapshot of a running system process consuming CPU and memory resources.
/// Used to identify culprit background tasks causing frequency spikes or thermal throttling.
/// </summary>
public sealed class ProcessActivity
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public double CpuPercent { get; init; }
    public double WorkingSetMb { get; init; }
    public int ThreadCount { get; init; }
    public bool IsSuspect { get; init; }
    public string ImpactReason { get; init; } = string.Empty;
}

/// <summary>
/// Summary of background processes attributed to a thermal or clock spike incident.
/// </summary>
public sealed class CulpritProcessSummary
{
    public bool HasCulprit { get; init; }
    public ProcessActivity? PrimaryCulprit { get; init; }
    public IReadOnlyList<ProcessActivity> TopProcesses { get; init; } = Array.Empty<ProcessActivity>();
    public string TechnicalDiagnosis { get; init; } = "No anomalous background process activity detected.";
}
