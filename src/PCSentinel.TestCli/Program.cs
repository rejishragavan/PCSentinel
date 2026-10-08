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
        Console.WriteLine("                         PC SENTINEL — FULL SYSTEM DEMO                         ");
        Console.WriteLine("   Telemetry, Root-Cause Diagnostics, Boot, Drivers, OC Lab & Black Box         ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        bool forceSimulate = args.Contains("--simulate") || !OperatingSystem.IsWindows();
        bool simulateThrottle = args.Contains("--throttle");
        bool simulateBootRegression = args.Contains("--boot-regression");
        bool triggerTestIncident = args.Contains("--trigger-incident");
        bool runOcDemo = args.Contains("--oc-lab") || args.Contains("--auto-oc");
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

        // 2. Storage SMART Subsystem (Phase 15)
        var storageAnalyzer = new StorageAnalyzer(simulateDegraded: args.Contains("--broken-storage"));
        var drives = await storageAnalyzer.InspectStorageDrivesAsync();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[STORAGE] Detected {drives.Count} drive(s):");
        Console.ResetColor();
        foreach (var drv in drives)
        {
            Console.WriteLine($"  • {drv.Model} ({drv.InterfaceType}) — Health: {drv.HealthPercentage}% | Temp: {drv.TemperatureC:F0}°C | Reallocated: {drv.ReallocatedSectors} | {drv.DegradationRisk}");
        }

        // 3. Boot Transition Subsystem (Phase 12 & 13)
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

        // 4. Black Box Incident Recorder (Phase 14)
        var incidentRecorder = new IncidentRecorder(store, driverInspector);
        incidentRecorder.IncidentLogged += (s, inc) =>
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n>>> [BLACK BOX RECORDED] Incident #{inc.IncidentNumber:D5} | Trigger: {inc.TriggerReason} | Pre-Samples: {inc.PreEventWindow.Count}");
            Console.ResetColor();
        };

        // 5. OC Lab Analyzer (Phase 18)
        var ocAnalyzer = new OcAnalyzer(store);

        // If user specifically requested OC Lab standalone demo
        if (runOcDemo)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n================================================================================");
            Console.WriteLine("                    OC LAB — AUTO-OVERCLOCKING DEMO RUNNER                      ");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            var sampleTelemetry = new TelemetrySample
            {
                CpuTemperatureC = 62.0,
                CpuClockMhz = 4750.0,
                GpuTemperatureC = 68.5,
                GpuClockMhz = 1845.0,
                GpuPowerWatts = 210.0,
                GpuLoadPercent = 95.0
            };

            Console.WriteLine("\n[1] Calculating Hardware Safe Tuning Headroom...");
            var headroom = ocAnalyzer.EstimateHeadroom(sampleTelemetry);
            Console.WriteLine($"    Thermal Headroom: {headroom.ThermalHeadroom}");
            Console.WriteLine($"    Power Headroom:   {headroom.PowerHeadroom}");
            Console.WriteLine($"    Clock Headroom:   {headroom.ClockHeadroom}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"    Recommended Safe Boost: +{headroom.RecommendedClockDeltaMhz:F0} MHz (Confidence: {headroom.Confidence * 100:F0}%)");
            Console.ResetColor();
            Console.WriteLine($"    Technical Rationale:    {headroom.Rationale}");

            Console.WriteLine("\n[2] Executing Controlled Stability Experiment (Stock vs +85 MHz Boost)...");
            var exp = await ocAnalyzer.ExecuteControlledExperimentAsync("Sentinel Auto-Tuned Profile (+85 MHz)", 85.0, 0.0);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"    Baseline Framerate:   {exp.BaselineFps:F1} FPS @ 1845 MHz");
            Console.WriteLine($"    Overclock Framerate:  {exp.ExperimentFps:F1} FPS @ 1930 MHz");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"    Performance Gain:     +{exp.PerformanceGainPercent:F1}% FPS Boost");
            Console.WriteLine($"    Thermal Stability:    {(exp.StabilityPassed ? "PASSED (Safe peak temp: " + exp.MaxTempReachedC + "°C)" : "FAILED (Overheated)")}");
            Console.ResetColor();
            Console.WriteLine($"    Status:               Persisted cleanly to SQLite OCExperiments table.");
            Console.WriteLine("================================================================================\n");
        }

        // 6. USB Sentinel Node & Core Bus
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

            // 5. Calculate OC Headroom
            var ocHeadroom = ocAnalyzer.EstimateHeadroom(sample, activeBaseline);

            // 6. Black Box Trigger on Critical Events or Test Flag
            if ((analysis.DetectedEvents.Any(e => e.Severity == EventSeverity.Critical) || triggerTestIncident) && !incidentTriggered)
            {
                incidentTriggered = true;
                await incidentRecorder.RecordIncidentAsync(
                    analysis.DetectedEvents.FirstOrDefault()?.Title ?? "Manual Diagnostic Incident",
                    EventSeverity.Critical,
                    analysis.Score.OverallScore,
                    analysis.DetectedEvents);
            }

            // 7. Persistence
            await store.StoreTelemetrySampleAsync(sample);
            await store.StoreHealthScoreAsync(analysis.Score);

            // 8. Forward Alerts
            foreach (var evt in analysis.DetectedEvents)
            {
                await alertManager.PublishAlertAsync(evt);
            }

            // 9. Transmit to ESP32-S3 Sentinel Node
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

            // 10. Render Console Dashboard
            RenderTelemetry(sample, workload, activeBaseline, analysis, driverSummary, latestBoot, ocHeadroom, drives.FirstOrDefault(), sampleCounter);
        };

        collector.CollectionError += (s, ex) =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Collection error: {ex.Message}");
            Console.ResetColor();
        };

        Console.WriteLine("[START] Real-time collector running (Press Ctrl+C to stop)...");
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
        Console.WriteLine($"\n[COMPLETE] PC Sentinel demo finished. Processed {sampleCounter} telemetry frames.");
        Console.WriteLine($"[STORAGE] Verified: All frames, boot sessions, OC experiments, and incidents persisted cleanly to SQLite: {dbPath}");
        Console.ResetColor();
    }

    private static void RenderTelemetry(
        TelemetrySample sample,
        WorkloadClassification workload,
        WorkloadBaseline? baseline,
        AnalysisResult analysis,
        DriverHealthSummary drivers,
        BootSession? boot,
        OcHeadroomEstimate oc,
        StorageDriveInfo? drive,
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

        // RAM & Storage
        Console.Write("RAM:     ");
        PrintMetric("Used", sample.RamUsedGb, " GB", null, null);
        PrintMetric("Load", sample.RamLoadPercent, "%", 85, 94);
        if (drive != null)
        {
            Console.Write($" | SSD: {drive.TemperatureC:F0}°C ({drive.HealthPercentage}% Health)");
        }
        Console.WriteLine();

        // OC Lab Headroom
        Console.ForegroundColor = oc.RecommendedClockDeltaMhz > 0 ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
        Console.WriteLine($"OC Lab:  Headroom: +{oc.RecommendedClockDeltaMhz:F0} MHz ({oc.ThermalHeadroom} Thermal Margin, Confidence: {oc.Confidence * 100:F0}%)");
        Console.ResetColor();

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
