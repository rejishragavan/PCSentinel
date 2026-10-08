using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Analysis;

/// <summary>
/// Deterministic rule-based diagnostic engine.
/// Evaluates telemetry signals simultaneously to detect anomalies, explain root causes,
/// and compute explainable component health scores.
/// </summary>
public sealed class RuleBasedHealthAnalyzer : IHealthAnalyzer
{
    public AnalysisResult Analyze(TelemetrySample current, WorkloadBaseline? baseline = null)
    {
        var events = new List<HealthEvent>();
        var deductions = new List<ScoreDeduction>();

        int cpuScore = 100;
        int gpuScore = 100;
        int memoryScore = 100;
        int storageScore = 100;

        // 1. GPU Thermal Throttling Check
        if (current.GpuTemperatureC.HasValue && current.GpuLoadPercent.HasValue)
        {
            var temp = current.GpuTemperatureC.Value;
            var hotspot = current.GpuHotspotC ?? temp;
            var load = current.GpuLoadPercent.Value;
            var clock = current.GpuClockMhz ?? 0;

            if (temp >= 85.0 || hotspot >= 100.0)
            {
                var evidence = $"GPU Core: {temp:F1}°C, Hotspot: {hotspot:F1}°C under {load:F0}% load. Clock throttled to {clock:F0} MHz.";
                events.Add(new HealthEvent
                {
                    Component = ComponentType.Gpu,
                    Severity = temp >= 88.0 ? EventSeverity.Critical : EventSeverity.Warning,
                    Type = HealthEventType.ThermalThrottling,
                    Title = "GPU Thermal Throttling Detected",
                    Evidence = evidence,
                    Confidence = 0.94,
                    Recommendation = "Inspect GPU cooling fans, check fan curves in BIOS/vendor software, and consider repasting thermal interface."
                });

                var points = temp >= 88.0 ? 25 : 15;
                gpuScore -= points;
                deductions.Add(new ScoreDeduction(ComponentType.Gpu, points, "Severe thermal limit reached (>85°C)"));
            }
        }

        // 2. Baseline-Relative Anomaly Detection (Phase 9 & 10)
        if (baseline != null && current.GpuTemperatureC.HasValue && current.GpuClockMhz.HasValue && current.GpuLoadPercent > 80)
        {
            var tempDiff = current.GpuTemperatureC.Value - baseline.AvgGpuTempC;
            var clockDiffPct = ((baseline.AvgGpuClockMhz - current.GpuClockMhz.Value) / baseline.AvgGpuClockMhz) * 100.0;

            if (tempDiff >= 9.0 && clockDiffPct >= 12.0)
            {
                var evidence = $"Baseline comparison ({baseline.ProfileName}): Temp is +{tempDiff:F1}°C above normal ({current.GpuTemperatureC.Value:F1}°C vs {baseline.AvgGpuTempC:F1}°C), Clock is -{clockDiffPct:F1}% lower ({current.GpuClockMhz.Value:F0} vs {baseline.AvgGpuClockMhz:F0} MHz).";
                events.Add(new HealthEvent
                {
                    Component = ComponentType.Gpu,
                    Severity = EventSeverity.Warning,
                    Type = HealthEventType.ThermalPerformanceAnomaly,
                    Title = "GPU Thermal Performance Deviation",
                    Evidence = evidence,
                    Confidence = 0.88,
                    Recommendation = "Cooling performance is degraded compared to historical baseline. Verify intake airflow, dust accumulation, or thermal paste degradation."
                });

                gpuScore -= 10;
                deductions.Add(new ScoreDeduction(ComponentType.Gpu, 10, "Deviated from historical profile: +10°C higher temp with clock drop"));
            }
        }

        // 3. CPU Thermal Check
        if (current.CpuTemperatureC.HasValue && current.CpuLoadPercent.HasValue)
        {
            var temp = current.CpuTemperatureC.Value;
            var load = current.CpuLoadPercent.Value;

            if (temp >= 95.0)
            {
                events.Add(new HealthEvent
                {
                    Component = ComponentType.Cpu,
                    Severity = EventSeverity.Critical,
                    Type = HealthEventType.ThermalThrottling,
                    Title = "CPU Junction Max Throttling Limit Reached",
                    Evidence = $"CPU package temperature reached {temp:F1}°C under {load:F0}% load.",
                    Confidence = 0.98,
                    Recommendation = "Check AIO pump RPM, ensure CPU cooler mount pressure is even, and check thermal compound application."
                });

                cpuScore -= 20;
                deductions.Add(new ScoreDeduction(ComponentType.Cpu, 20, "CPU reached TjMax (>=95°C)"));
            }
            else if (temp >= 85.0 && load > 50.0)
            {
                cpuScore -= 8;
                deductions.Add(new ScoreDeduction(ComponentType.Cpu, 8, "Elevated CPU temperature under moderate load"));
            }
        }

        // 4. Memory Saturation Check
        if (current.RamLoadPercent.HasValue && current.RamLoadPercent.Value >= 95.0)
        {
            events.Add(new HealthEvent
            {
                Component = ComponentType.Memory,
                Severity = EventSeverity.Warning,
                Type = HealthEventType.PerformanceDeviation,
                Title = "Critical RAM Saturation",
                Evidence = $"RAM utilization is at {current.RamLoadPercent.Value:F1}%. Disk paging may cause system hitches.",
                Confidence = 0.90,
                Recommendation = "Review processes with high private commit bytes in Task Manager or upgrade system RAM capacity."
            });

            memoryScore -= 15;
            deductions.Add(new ScoreDeduction(ComponentType.Memory, 15, "RAM memory utilization > 95%"));
        }

        // Clamp component scores to [0, 100]
        cpuScore = Math.Clamp(cpuScore, 0, 100);
        gpuScore = Math.Clamp(gpuScore, 0, 100);
        memoryScore = Math.Clamp(memoryScore, 0, 100);
        storageScore = Math.Clamp(storageScore, 0, 100);

        // Weighted overall score: 35% CPU, 35% GPU, 15% RAM, 15% Storage
        int overallScore = (int)Math.Round(
            (cpuScore * 0.35) +
            (gpuScore * 0.35) +
            (memoryScore * 0.15) +
            (storageScore * 0.15)
        );

        var score = new HealthScore
        {
            OverallScore = overallScore,
            CpuScore = cpuScore,
            GpuScore = gpuScore,
            MemoryScore = memoryScore,
            StorageScore = storageScore,
            Deductions = deductions
        };

        return new AnalysisResult(score, events);
    }
}
