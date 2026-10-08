namespace PCSentinel.Core.Models;

public enum HeadroomLevel
{
    Low,
    Medium,
    High
}

/// <summary>
/// Estimated overclocking and frequency boost headroom with confidence factors.
/// </summary>
public record OcHeadroomEstimate
{
    public ComponentType Component { get; init; } = ComponentType.Gpu;
    public HeadroomLevel ThermalHeadroom { get; init; } = HeadroomLevel.High;
    public HeadroomLevel PowerHeadroom { get; init; } = HeadroomLevel.Medium;
    public HeadroomLevel ClockHeadroom { get; init; } = HeadroomLevel.High;

    public double RecommendedClockDeltaMhz { get; init; } = 95.0; // e.g. +95 MHz
    public double RecommendedVoltageDeltaMv { get; init; } = 0.0;  // 0mV (undervolt/stock voltage safety first)
    public double Confidence { get; init; } = 0.84;
    public double CurrentPeakTempC { get; init; } = 68.0;
    public double MaxSafeTempC { get; init; } = 82.0;
    public double EstimatedPowerDeltaW { get; init; } = 15.0;
    public string Rationale { get; init; } = string.Empty;
}

/// <summary>
/// Controlled benchmark run recording scores, clocks, thermals, and framerates.
/// </summary>
public record BenchmarkResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string BenchmarkName { get; init; } = "Sentinel 3D Stability Benchmark";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public int DurationSeconds { get; init; } = 30;
    public int Score { get; init; }
    public double AverageFps { get; init; }
    public double AvgCpuTempC { get; init; }
    public double AvgGpuTempC { get; init; }
    public double AvgGpuClockMhz { get; init; }
    public double PeakPowerWatts { get; init; }
    public bool IsOverclocked { get; init; }
    public double ClockOffsetMhz { get; init; }
}

/// <summary>
/// Historical record of a controlled tuning experiment.
/// </summary>
public record OcExperiment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int ExperimentNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string SettingLabel { get; init; } = "Stock Baseline";
    public double GpuClockOffsetMhz { get; init; }
    public double GpuVoltageOffsetMv { get; init; }
    public bool StabilityPassed { get; init; } = true;
    public double BaselineFps { get; init; }
    public double ExperimentFps { get; init; }
    public double PerformanceGainPercent { get; init; }
    public double MaxTempReachedC { get; init; }
    public string Notes { get; init; } = string.Empty;
}
