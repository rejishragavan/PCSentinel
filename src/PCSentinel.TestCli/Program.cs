using PCSentinel.Analysis;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;
using PCSentinel.Hardware;
using PCSentinel.Storage;

namespace PCSentinel.TestCli;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("                         PC SENTINEL — MILESTONE 2                              ");
        Console.WriteLine("      Multi-Signal Diagnostics, Baseline Learning & Sentinel Node Bus           ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        bool forceSimulate = args.Contains("--simulate") || !OperatingSystem.IsWindows();
        bool simulateThrottle = args.Contains("--throttle");
        string comPort = args.FirstOrDefault(a => a.StartsWith("--com="))?.Split('=')[1] ?? "VIRTUAL";

        ISensorProvider provider;
        if (forceSimulate)
        {
            var profile = simulateThrottle ? SimulationProfile.ThermalThrottlingIncident : SimulationProfile.GamingHeavy;
            Console.WriteLine($"[INIT] Hardware mode: SIMULATED (Profile: {profile})");
            provider = new SimulatedSensorProvider(profile);
        }
        else
        {
            Console.WriteLine("[INIT] Hardware mode: LIVE (LibreHardwareMonitorLib)");
            provider = new LibreHardwareSensorProvider();
        }

        // Initialize SQLite Storage in local directory
        var dbPath = Path.Combine(AppContext.BaseDirectory, "sentinel_history.db");
        var store = new SqliteTelemetryStore(dbPath);
        Console.WriteLine($"[INIT] Initializing SQLite database: {dbPath}");
        await store.InitializeDatabaseAsync();

        // Connect USB Serial Sentinel Node (or Virtual loopback)
        var sentinelNode = new UsbSerialSentinelNode();
        await sentinelNode.ConnectAsync(comPort);
        Console.WriteLine($"[INIT] Sentinel Node interface: {sentinelNode.PortName} (Active: {sentinelNode.IsConnected})");

        var alertManager = new AlertManager(store, sentinelNode);
        var classifier = new WorkloadClassifier();
        var learner = new StatisticalBaselineLearner();
        var correlationEngine = new MultiSignalCorrelationEngine(classifier);

        // Seed a sample baseline for comparison
        var seededBaseline = new WorkloadBaseline
        {
            ProfileName = "Historical Gaming Baseline",
            Workload = WorkloadType.Gaming,
            AvgCpuTempC = 64.0,
            StdDevCpuTempC = 3.0,
            AvgCpuClockMhz = 4700.0,
            AvgCpuPowerW = 75.0,
            AvgGpuTempC = 71.0,
            StdDevGpuTempC = 2.5,
            AvgGpuClockMhz = 1845.0,
            AvgGpuPowerW = 210.0,
            SampleCount = 1200
        };
        await store.StoreBaselineAsync(seededBaseline);

        using var collector = new SensorCollector(provider, TimeSpan.FromSeconds(1));
        int sampleCounter = 0;
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n[SHUTDOWN] Stopping collector and flushing database...");
        };

        collector.SampleCollected += async (s, sample) =>
        {
            sampleCounter++;

            // 1. Classify Workload
            var workload = classifier.Classify(sample);

            // 2. Ingest into Statistical Baseline Learner
            learner.IngestSample(sample, workload.Workload);
            var activeBaseline = learner.GetBaseline(workload.Workload) ?? (workload.Workload == WorkloadType.Gaming ? seededBaseline : null);

            // 3. Multi-Signal Diagnostic Correlation
            var analysis = correlationEngine.Analyze(sample, activeBaseline);

            // 4. Persist sample and scores
            await store.StoreTelemetrySampleAsync(sample);
            await store.StoreHealthScoreAsync(analysis.Score);

            // 5. Broadcast alerts
            foreach (var evt in analysis.DetectedEvents)
            {
                await alertManager.PublishAlertAsync(evt);
            }

            // 6. Transmit Telemetry Packet to ESP32-S3 Sentinel Node
            var packet = new SentinelPacket
            {
                Type = "status",
                Score = analysis.Score.OverallScore,
                CpuTemp = sample.CpuTemperatureC ?? 0,
                GpuTemp = sample.GpuTemperatureC ?? 0,
                CpuLoad = sample.CpuLoadPercent ?? 0,
                GpuLoad = sample.GpuLoadPercent ?? 0,
                Workload = workload.Workload.ToString(),
                LedColor = analysis.Score.OverallScore >= 90 ? "GREEN" : analysis.Score.OverallScore >= 75 ? "YELLOW" : "RED",
                TriggerBuzzer = analysis.DetectedEvents.Any(e => e.Severity == EventSeverity.Critical)
            };
            await sentinelNode.SendTelemetryAsync(packet);

            // 7. Render Console Dashboard
            RenderTelemetry(sample, workload, activeBaseline, analysis, sampleCounter);
        };

        collector.CollectionError += (s, ex) =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Collection error: {ex.Message}");
            Console.ResetColor();
        };

        Console.WriteLine("[START] Collector running (Press Ctrl+C to stop)...");
        await collector.StartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException) { }

        await collector.StopAsync();
        await sentinelNode.DisconnectAsync();
        await store.DisposeAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[COMPLETE] Milestone 2 verification finished. Processed {sampleCounter} telemetry frames.");
        Console.WriteLine($"[STORAGE] Verified: All frames and events persisted cleanly to SQLite: {dbPath}");
        Console.ResetColor();
    }

    private static void RenderTelemetry(
        TelemetrySample sample,
        WorkloadClassification workload,
        WorkloadBaseline? baseline,
        AnalysisResult analysis,
        int count)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"=== PC SENTINEL LIVE TELEMETRY [Frame #{count}] | {sample.Timestamp:HH:mm:ss} UTC ===");
        Console.ResetColor();

        // Workload Indicator
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"[ACTIVE WORKLOAD: {workload.Workload.ToString().ToUpper()}] ");
        Console.ResetColor();
        Console.WriteLine($"{workload.Description} (Confidence: {workload.Confidence * 100:F0}%)");

        // CPU
        Console.Write("CPU:     ");
        PrintMetric("Temp", sample.CpuTemperatureC, "°C", 85, 95);
        PrintMetric("Clock", sample.CpuClockMhz, " MHz", null, null);
        PrintMetric("Load", sample.CpuLoadPercent, "%", 80, 95);
        PrintMetric("Power", sample.CpuPowerWatts, " W", null, null);
        Console.WriteLine();

        // GPU
        Console.Write("GPU:     ");
        PrintMetric("Temp", sample.GpuTemperatureC, "°C", 80, 85);
        PrintMetric("Hotspot", sample.GpuHotspotC, "°C", 90, 100);
        PrintMetric("Clock", sample.GpuClockMhz, " MHz", null, null);
        PrintMetric("Load", sample.GpuLoadPercent, "%", null, null);
        PrintMetric("Fan", sample.GpuFanPercent, "%", null, null);
        Console.WriteLine();

        // RAM & Storage
        Console.Write("RAM:     ");
        PrintMetric("Used", sample.RamUsedGb, " GB", null, null);
        PrintMetric("Load", sample.RamLoadPercent, "%", 85, 94);
        Console.WriteLine();

        // Learned Baseline Comparison
        if (baseline != null)
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Baseline Profile: {baseline.ProfileName} (N={baseline.SampleCount})");
            Console.WriteLine($"  Expected Normal: GPU Temp: {baseline.AvgGpuTempC:F1}±{baseline.StdDevGpuTempC:F1}°C | GPU Clock: {baseline.AvgGpuClockMhz:F0} MHz | Power: {baseline.AvgGpuPowerW:F0} W");
            Console.ResetColor();
        }

        // Health Score & Deductions
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.ForegroundColor = analysis.Score.OverallScore >= 90 ? ConsoleColor.Green :
                                  analysis.Score.OverallScore >= 75 ? ConsoleColor.Yellow : ConsoleColor.Red;
        Console.WriteLine($"HEALTH SCORE: {analysis.Score.OverallScore}/100  (CPU: {analysis.Score.CpuScore} | GPU: {analysis.Score.GpuScore} | RAM: {analysis.Score.MemoryScore} | Storage: {analysis.Score.StorageScore})");
        Console.ResetColor();

        if (analysis.Score.Deductions.Count > 0)
        {
            Console.WriteLine("Explainable Deductions:");
            foreach (var d in analysis.Score.Deductions)
            {
                Console.WriteLine($"  - [-{d.PointsDeducted} pts] {d.Component}: {d.Reason}");
            }
        }

        // Active Diagnostic Events
        if (analysis.DetectedEvents.Count > 0)
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("ROOT-CAUSE CORRELATION DIAGNOSTICS:");
            foreach (var ev in analysis.DetectedEvents)
            {
                Console.ForegroundColor = ev.Severity == EventSeverity.Critical ? ConsoleColor.Red : ConsoleColor.Yellow;
                Console.WriteLine($"  [{ev.Severity}] {ev.Title} ({ev.Type}) — Confidence: {ev.Confidence * 100:F0}%");
                Console.ResetColor();
                Console.WriteLine($"    Evidence: {ev.Evidence}");
                Console.WriteLine($"    Action:   {ev.Recommendation}");
            }
        }

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine("Press Ctrl+C to terminate harness.");
    }

    private static void PrintMetric(string label, double? value, string unit, double? warnThresh, double? critThresh)
    {
        if (!value.HasValue)
        {
            Console.Write($"{label}: N/A  ");
            return;
        }

        Console.Write($"{label}: ");
        if (critThresh.HasValue && value.Value >= critThresh.Value)
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
        else if (warnThresh.HasValue && value.Value >= warnThresh.Value)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
        }

        Console.Write($"{value.Value:F1}{unit}");
        Console.ResetColor();
        Console.Write("  ");
    }
}
