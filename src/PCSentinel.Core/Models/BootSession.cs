namespace PCSentinel.Core.Models;

/// <summary>
/// Breakdown of Windows boot duration, driver initialization, and startup phases.
/// Corresponds to Windows Performance Diagnostics Event 100 (On/Off - Boot).
/// </summary>
public record BootSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int BootId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // Timing in milliseconds
    public double TotalBootDurationMs { get; init; }
    public double MainPathBootDurationMs { get; init; }
    public double DriverInitDurationMs { get; init; }
    public double PostBootDurationMs { get; init; }
    public double SmssInitDurationMs { get; init; }

    // Historical comparison
    public bool IsRegression { get; init; }
    public double? NormalBootDurationMs { get; init; }
    public double? RegressionDeltaMs { get; init; }
    public string? RegressionExplanation { get; init; }
    public string SlowestSubsystem { get; init; } = "None";
}
