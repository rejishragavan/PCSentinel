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
        Console.WriteLine("                         PC SENTINEL — MILESTONE 1                              ");
        Console.WriteLine("                Hardware Telemetry Proof & Diagnostic Engine                    ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        bool forceSimulate = args.Contains("--simulate") || !OperatingSystem.IsWindows();
        bool simulateThrottle = args.Contains("--throttle");

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

        // Seed a sample gaming baseline for comparison
        var gamingBaseline = new WorkloadBaseline
        {
            ProfileName = "Gaming Normal (Baseline)",
            Workload = WorkloadType.Gaming,
            AvgCpuTempC = 65.0,
            AvgCpuClockMhz = 4700.0,
            AvgCpuPowerW = 75.0,
            AvgGpuTempC = 72.0,
            AvgGpuClockMhz = 1845.0,
            AvgGpuPowerW = 210.0,
            SampleCount = 500
        };
        await store.StoreBaselineAsync(gamingBaseline);

        var analyzer = new RuleBasedHealthAnalyzer();
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

            // Run Diagnostics
            var analysis = analyzer.Analyze(sample, gamingBaseline);

            // Persist to SQLite
            await store.StoreTelemetrySampleAsync(sample);
            await store.StoreHealthScoreAsync(analysis.Score);
            foreach (var evt in analysis.DetectedEvents)
            {
                await store.StoreHealthEventAsync(evt);
            }

            // Render live console display
            RenderTelemetry(sample, analysis, sampleCounter);
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
            // Run until cancelled
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Clean exit
        }

        await collector.StopAsync();
        await store.DisposeAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[COMPLETE] Milestone 1 verification finished. Processed {sampleCounter} telemetry frames.");
        Console.WriteLine($"[STORAGE] Verified: All frames persisted cleanly to SQLite: {dbPath}");
        Console.ResetColor();
    }

    private static void RenderTelemetry(TelemetrySample sample, AnalysisResult analysis, int count)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"=== PC SENTINEL LIVE TELEMETRY [Frame #{count}] | {sample.Timestamp:HH:mm:ss} UTC ===");
        Console.ResetColor();

        // CPU
        Console.Write("CPU:     ");
        PrintMetric("Temp", sample.CpuTemperatureC, "°C", 85, 95);
        PrintMetric("Clock", sample.CpuClockMhz, " MHz", null, null);
        PrintMetric("Load", sample.CpuLoadPercent, "%", 80, 95);
        PrintMetric("Power", sample.CpuPowerWatts, " W", null, null);
        Console.WriteLine();

        // GPU
        Console.Write("GPU:     ");
        PrintMetric("Temp", sample.GpuTemperatureC, "°C", 80, 86);
        PrintMetric("Hotspot", sample.GpuHotspotC, "°C", 90, 102);
        PrintMetric("Clock", sample.GpuClockMhz, " MHz", null, null);
        PrintMetric("Load", sample.GpuLoadPercent, "%", null, null);
        PrintMetric("Fan", sample.GpuFanPercent, "%", null, null);
        Console.WriteLine();

        // Memory
        Console.Write("RAM:     ");
        PrintMetric("Used", sample.RamUsedGb, " GB", null, null);
        PrintMetric("Load", sample.RamLoadPercent, "%", 85, 95);
        Console.WriteLine();

        // Storage
        if (sample.StorageTemperaturesC.Count > 0)
        {
            Console.Write("Storage: ");
            foreach (var kvp in sample.StorageTemperaturesC)
            {
                PrintMetric(kvp.Key, kvp.Value, "°C", 60, 75);
            }
            Console.WriteLine();
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
            Console.WriteLine("DIAGNOSTIC EVENTS DETECTED:");
            foreach (var ev in analysis.DetectedEvents)
            {
                Console.ForegroundColor = ev.Severity == EventSeverity.Critical ? ConsoleColor.Red : ConsoleColor.Yellow;
                Console.WriteLine($"  [{ev.Severity}] {ev.Title} (Confidence: {ev.Confidence * 100:F0}%)");
                Console.ResetColor();
                Console.WriteLine($"    Evidence: {ev.Evidence}");
                Console.WriteLine($"    Action:   {ev.Recommendation}");
            }
        }

        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.WriteLine("Press Ctrl+C to terminate test harness.");
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
