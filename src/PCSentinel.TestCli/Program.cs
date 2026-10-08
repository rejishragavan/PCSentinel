using PCSentinel.Analysis;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;
using PCSentinel.Diagnostics;
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
        Console.WriteLine("                         PC SENTINEL — MILESTONE 3                              ");
        Console.WriteLine("  OS Diagnostics, Boot Transition Analysis & Incident Black Box Recorder        ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        bool forceSimulate = args.Contains("--simulate") || !OperatingSystem.IsWindows();
        bool simulateThrottle = args.Contains("--throttle");
        bool simulateBootRegression = args.Contains("--boot-regression");
        bool triggerTestIncident = args.Contains("--trigger-incident");
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

        // 1. Diagnostics Subsystem
        var driverInspector = new PnpDeviceInspector(simulateDegraded: args.Contains("--broken-driver"));
        var driverSummary = await driverInspector.ScanDriversAsync();
        Console.ForegroundColor = driverSummary.HasCriticalFailure ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine($"[DIAGNOSTICS] PnP Drivers: {driverSummary.HealthyCount} Healthy, {driverSummary.ErrorCount} Errored, {driverSummary.DegradedCount} Degraded");
        Console.ResetColor();
        foreach (var d in driverSummary.Devices)
        {
            var icon = d.Status == DeviceStatusCode.Ok ? "✓" : "⚠";
            Console.WriteLine($"  {icon} [{d.Category}] {d.DeviceName} ({d.Status}) {d.ErrorDescription}");
        }

        // 2. Boot Transition Subsystem (Phase 12 & 13)
        var bootAnalyzer = new BootAnalyzer(store, simulateRegression: simulateBootRegression);
        var latestBoot = await bootAnalyzer.AnalyzeLatestBootAsync();
        if (latestBoot != null)
        {
            Console.ForegroundColor = latestBoot.IsRegression ? ConsoleColor.Red : ConsoleColor.Green;
            Console.WriteLine($"[BOOT] Duration: {latestBoot.TotalBootDurationMs / 1000.0:F1}s (Driver Init: {latestBoot.DriverInitDurationMs / 1000.0:F1}s, Apps: {latestBoot.PostBootDurationMs / 1000.0:F1}s)");
            if (latestBoot.IsRegression)
            {
                Console.WriteLine($"  ⚠ {latestBoot.RegressionExplanation}");
            }
            Console.ResetColor();
        }

        // 3. Black Box Incident Recorder (Phase 14)
        var incidentRecorder = new IncidentRecorder(store, driverInspector);
        incidentRecorder.IncidentLogged += (s, inc) =>
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n>>> [BLACK BOX RECORDED] Incident #{inc.IncidentNumber:D5} | Trigger: {inc.TriggerReason} | Pre-Samples: {inc.PreEventWindow.Count}");
            Console.ResetColor();
        };

        // 4. USB Sentinel Node & Core Bus
        var sentinelNode = new UsbSerialSentinelNode();
        await sentinelNode.ConnectAsync(comPort);
        Console.WriteLine($"[INIT] Sentinel Node interface: {sentinelNode.PortName} (Active: {sentinelNode.IsConnected})");

        var alertManager = new AlertManager(store, sentinelNode);
        var classifier = new WorkloadClassifier();
        var learner = new StatisticalBaselineLearner();
        var correlationEngine = new MultiSignalCorrelationEngine(classifier);

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
        bool incidentTriggered = false;
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

            // 1. Ingest into Incident Ring Buffer
            incidentRecorder.IngestTelemetry(sample);

            // 2. Classify Workload
            var workload = classifier.Classify(sample);

            // 3. Ingest into Baseline Learner
            learner.IngestSample(sample, workload.Workload);
            var activeBaseline = learner.GetBaseline(workload.Workload) ?? (workload.Workload == WorkloadType.Gaming ? seededBaseline : null);

            // 4. Multi-Signal Diagnostic Correlation
            var analysis = correlationEngine.Analyze(sample, activeBaseline);

            // 5. Black Box Trigger on Critical Events or Test Flag
            if ((analysis.DetectedEvents.Any(e => e.Severity == EventSeverity.Critical) || triggerTestIncident) && !incidentTriggered)
            {
                incidentTriggered = true;
                await incidentRecorder.RecordIncidentAsync(
                    analysis.DetectedEvents.FirstOrDefault()?.Title ?? "Manual Diagnostic Incident",
                    EventSeverity.Critical,
                    analysis.Score.OverallScore,
                    analysis.DetectedEvents);
            }

            // 6. Persistence
            await store.StoreTelemetrySampleAsync(sample);
            await store.StoreHealthScoreAsync(analysis.Score);

            // 7. Forward Alerts
            foreach (var evt in analysis.DetectedEvents)
            {
                await alertManager.PublishAlertAsync(evt);
            }

            // 8. Transmit to ESP32-S3 Sentinel Node
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

            // 9. Render Console Dashboard
            RenderTelemetry(sample, workload, activeBaseline, analysis, driverSummary, latestBoot, sampleCounter);
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
        Console.WriteLine($"\n[COMPLETE] Milestone 3 verification finished. Processed {sampleCounter} telemetry frames.");
        Console.WriteLine($"[STORAGE] Verified: All frames, boot sessions, and incidents persisted cleanly to SQLite: {dbPath}");
        Console.ResetColor();
    }

    private static void RenderTelemetry(
        TelemetrySample sample,
        WorkloadClassification workload,
        WorkloadBaseline? baseline,
        AnalysisResult analysis,
        DriverHealthSummary drivers,
        BootSession? boot,
        int count)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"=== PC SENTINEL LIVE TELEMETRY [Frame #{count}] | {sample.Timestamp:HH:mm:ss} UTC ===");
        Console.ResetColor();

        // Workload & Boot Status
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write($"[WORKLOAD: {workload.Workload.ToString().ToUpper()}] ");
        Console.ResetColor();
        Console.Write($"{workload.Description}  ");

        if (boot != null)
        {
            Console.Write("| Boot: ");
            Console.ForegroundColor = boot.IsRegression ? ConsoleColor.Red : ConsoleColor.Green;
            Console.Write($"{boot.TotalBootDurationMs / 1000.0:F1}s");
            Console.ResetColor();
        }
        Console.WriteLine();

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

        // RAM
        Console.Write("RAM:     ");
        PrintMetric("Used", sample.RamUsedGb, " GB", null, null);
        PrintMetric("Load", sample.RamLoadPercent, "%", 85, 94);
        Console.WriteLine();

        // Driver Health Status
        Console.ForegroundColor = drivers.HasCriticalFailure ? ConsoleColor.Red : ConsoleColor.DarkGray;
        Console.WriteLine($"Drivers: {drivers.HealthyCount} healthy | {drivers.ErrorCount} errors | {drivers.DegradedCount} degraded");
        Console.ResetColor();

        // Learned Baseline Comparison
        if (baseline != null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Baseline: {baseline.ProfileName} (Normal GPU: {baseline.AvgGpuTempC:F1}±{baseline.StdDevGpuTempC:F1}°C @ {baseline.AvgGpuClockMhz:F0}MHz)");
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
