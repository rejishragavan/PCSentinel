using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Analysis;

/// <summary>
/// Advanced Phase 10 Root-Cause Correlation Engine.
/// Correlates multiple concurrent signals (thermal, power, clocks, fans, baseline deviations)
/// to diagnose what is wrong with the PC and explain why.
/// </summary>
public sealed class MultiSignalCorrelationEngine : IHealthAnalyzer
{
    private readonly IWorkloadClassifier _classifier;

    public MultiSignalCorrelationEngine(IWorkloadClassifier? classifier = null)
    {
        _classifier = classifier ?? new WorkloadClassifier();
    }

    public AnalysisResult Analyze(TelemetrySample current, WorkloadBaseline? baseline = null)
    {
        var events = new List<HealthEvent>();
        var deductions = new List<ScoreDeduction>();

        int cpuScore = 100;
        int gpuScore = 100;
        int memoryScore = 100;
        int storageScore = 100;

        var classification = _classifier.Classify(current);

        // --- SIGNAL CORRELATION 1: GPU Thermal vs Power Bottleneck ---
        if (current.GpuLoadPercent.HasValue && current.GpuLoadPercent.Value >= 80.0)
        {
            var temp = current.GpuTemperatureC ?? 0.0;
            var hotspot = current.GpuHotspotC ?? temp;
            var clock = current.GpuClockMhz ?? 0.0;
            var power = current.GpuPowerWatts ?? 0.0;
            var fan = current.GpuFanPercent ?? 0.0;

            // Pattern 1A: Thermal Limitation (Hot + High Fan + Downclocked)
            if (temp >= 83.0 || hotspot >= 98.0)
            {
                var evidence = $"GPU Load {current.GpuLoadPercent.Value:F0}%, Core Temp {temp:F1}°C, Hotspot {hotspot:F1}°C, Fan {fan:F0}%, Clock throttled to {clock:F0} MHz.";
                events.Add(new HealthEvent
                {
                    Component = ComponentType.Gpu,
                    Severity = (temp >= 88.0 || hotspot >= 105.0) ? EventSeverity.Critical : EventSeverity.Warning,
                    Type = HealthEventType.ThermalThrottling,
                    Title = "GPU Thermal Throttling Confirmed",
                    Evidence = evidence,
                    Confidence = 0.95,
                    Recommendation = "Intake/exhaust airflow or heatsink contact is deficient. Check for dust clogging, evaluate fan curves, and inspect thermal pads/paste."
                });

                var deduction = temp >= 88.0 ? 25 : 15;
                gpuScore -= deduction;
                deductions.Add(new ScoreDeduction(ComponentType.Gpu, deduction, $"GPU thermal limit exceeded (Core: {temp:F0}°C, Hotspot: {hotspot:F0}°C)"));
            }
            // Pattern 1B: Power Limit / VRM Throttling (Cool Thermals + Depressed Clock + Low Power)
            else if (temp < 72.0 && clock < 1550.0 && power > 0 && power < 170.0 && classification.Workload == WorkloadType.Gaming)
            {
                var evidence = $"GPU Load {current.GpuLoadPercent.Value:F0}% at cool temp ({temp:F1}°C), but Clock is only {clock:F0} MHz with Power restricted to {power:F1}W.";
                events.Add(new HealthEvent
                {
                    Component = ComponentType.Gpu,
                    Severity = EventSeverity.Warning,
                    Type = HealthEventType.PowerThrottling,
                    Title = "GPU Power Limit / VRM Throttling Suspected",
                    Evidence = evidence,
                    Confidence = 0.86,
                    Recommendation = "GPU clocks are downclocked despite low temperature. Check GPU power target sliders, PCIe power cables, or motherboard BIOS power delivery limits."
                });

                gpuScore -= 12;
                deductions.Add(new ScoreDeduction(ComponentType.Gpu, 12, "GPU power delivery bottleneck under sustained load"));
            }
        }

        // --- SIGNAL CORRELATION 2: Historical Baseline Deviation (Phase 9) ---
        if (baseline != null && current.GpuTemperatureC.HasValue && current.GpuClockMhz.HasValue && classification.Workload == baseline.Workload)
        {
            var temp = current.GpuTemperatureC.Value;
            var clock = current.GpuClockMhz.Value;
            var tempDeviation = temp - baseline.AvgGpuTempC;
            var clockDeviation = baseline.AvgGpuClockMhz - clock;

            // Threshold: Temp >= Mean + 2*StdDev (or +8°C) AND Clock down by > 10%
            var stdDevThreshold = Math.Max(2.0 * baseline.StdDevGpuTempC, 8.0);
            if (tempDeviation >= stdDevThreshold && clockDeviation > (0.10 * baseline.AvgGpuClockMhz))
            {
                var evidence = $"Compared to learned baseline for '{baseline.ProfileName}': Temp is +{tempDeviation:F1}°C higher ({temp:F1}°C vs baseline {baseline.AvgGpuTempC:F1}°C), Clock degraded -{clockDeviation:F0} MHz ({clock:F0} vs baseline {baseline.AvgGpuClockMhz:F0} MHz).";
                events.Add(new HealthEvent
                {
                    Component = ComponentType.Gpu,
                    Severity = EventSeverity.Warning,
                    Type = HealthEventType.CoolingDegradation,
                    Title = "Hardware Cooling Degradation Trend",
                    Evidence = evidence,
                    Confidence = 0.89,
                    Recommendation = "Cooling efficiency has statistically decayed compared to historical baseline. Consider clearing dust filters or replacing thermal compound."
                });

                gpuScore -= 10;
                deductions.Add(new ScoreDeduction(ComponentType.Gpu, 10, "Deviated from learned baseline (+8°C higher temp under identical workload)"));
            }
        }

        // --- SIGNAL CORRELATION 3: CPU Package TjMax Throttling ---
        if (current.CpuTemperatureC.HasValue && current.CpuLoadPercent.HasValue)
        {
            var temp = current.CpuTemperatureC.Value;
            var load = current.CpuLoadPercent.Value;
            var clock = current.CpuClockMhz ?? 0.0;

            if (temp >= 95.0)
            {
                var evidence = $"CPU Package Temp reached {temp:F1}°C (TjMax limit) under {load:F0}% load at {clock:F0} MHz.";
                events.Add(new HealthEvent
                {
                    Component = ComponentType.Cpu,
                    Severity = EventSeverity.Critical,
                    Type = HealthEventType.ThermalThrottling,
                    Title = "CPU TjMax Thermal Throttling",
                    Evidence = evidence,
                    Confidence = 0.98,
                    Recommendation = "Ensure AIO liquid cooler pump is circulating, check radiator fan RPM, and verify mounting pressure."
                });

                cpuScore -= 22;
                deductions.Add(new ScoreDeduction(ComponentType.Cpu, 22, "CPU reached thermal cutoff limit (>= 95°C)"));
            }
            else if (temp >= 85.0 && load > 60.0)
            {
                cpuScore -= 8;
                deductions.Add(new ScoreDeduction(ComponentType.Cpu, 8, "Elevated CPU temperature under sustained workload"));
            }
        }

        // --- SIGNAL CORRELATION 4: RAM Saturation ---
        if (current.RamLoadPercent.HasValue && current.RamLoadPercent.Value >= 94.0)
        {
            events.Add(new HealthEvent
            {
                Component = ComponentType.Memory,
                Severity = EventSeverity.Warning,
                Type = HealthEventType.PerformanceDeviation,
                Title = "Critical Memory Pressure",
                Evidence = $"Physical RAM saturation at {current.RamLoadPercent.Value:F1}%. Page file swap activity will cause system stutters.",
                Confidence = 0.92,
                Recommendation = "Close background processes consuming working set memory or upgrade system RAM capacity."
            });

            memoryScore -= 15;
            deductions.Add(new ScoreDeduction(ComponentType.Memory, 15, "RAM saturation > 94%"));
        }

        // Clamp component scores [0, 100]
        cpuScore = Math.Clamp(cpuScore, 0, 100);
        gpuScore = Math.Clamp(gpuScore, 0, 100);
        memoryScore = Math.Clamp(memoryScore, 0, 100);
        storageScore = Math.Clamp(storageScore, 0, 100);

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
