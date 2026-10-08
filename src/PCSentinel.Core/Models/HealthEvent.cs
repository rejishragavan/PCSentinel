namespace PCSentinel.Core.Models;

/// <summary>
/// Detected diagnostic or health event with evidence and confidence score.
/// </summary>
public record HealthEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public ComponentType Component { get; init; }
    public EventSeverity Severity { get; init; }
    public HealthEventType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public double Confidence { get; init; } = 1.0; // 0.0 to 1.0
    public string Recommendation { get; init; } = string.Empty;
}

public record ScoreDeduction(ComponentType Component, int PointsDeducted, string Reason);

/// <summary>
/// Explainable component-level health score breakdown (0-100).
/// </summary>
public record HealthScore
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public int OverallScore { get; init; } = 100;
    public int CpuScore { get; init; } = 100;
    public int GpuScore { get; init; } = 100;
    public int MemoryScore { get; init; } = 100;
    public int StorageScore { get; init; } = 100;
    public IReadOnlyList<ScoreDeduction> Deductions { get; init; } = Array.Empty<ScoreDeduction>();
}
