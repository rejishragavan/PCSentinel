using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PCSentinel.Analysis;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;
using PCSentinel.Diagnostics;
using PCSentinel.Hardware;
using PCSentinel.Storage;

namespace PCSentinel.Service;

/// <summary>
/// Headless 24/7 background worker service (Phase 20).
/// Runs continuously as a Windows Service, recording telemetry, learning baselines,
/// detecting anomalies, capturing black-box crashes, and streaming to the ESP32 node.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private ITelemetryStore? _store;
    private ISensorProvider? _provider;
    private SensorCollector? _collector;
    private UsbSerialSentinelNode? _sentinelNode;
    private AlertManager? _alertManager;
    private WorkloadClassifier? _classifier;
    private StatisticalBaselineLearner? _baselineLearner;
    private MultiSignalCorrelationEngine? _correlationEngine;
    private IncidentRecorder? _incidentRecorder;
    private PnpDeviceInspector? _driverInspector;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PC Sentinel Background Service is starting...");

        _store = new SqliteTelemetryStore();
        await _store.InitializeDatabaseAsync(cancellationToken);

        _driverInspector = new PnpDeviceInspector();
        _incidentRecorder = new IncidentRecorder(_store, _driverInspector);
        _classifier = new WorkloadClassifier();
        _baselineLearner = new StatisticalBaselineLearner();
        _correlationEngine = new MultiSignalCorrelationEngine(_classifier);

        _sentinelNode = new UsbSerialSentinelNode();
        await _sentinelNode.ConnectAsync("VIRTUAL", cancellationToken);

        _alertManager = new AlertManager(_store, _sentinelNode);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                _provider = new LibreHardwareSensorProvider();
                _logger.LogInformation("Loaded ring-0 LibreHardwareMonitor provider.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load ring-0 driver: {Message}. Falling back to simulator.", ex.Message);
                _provider = new SimulatedSensorProvider(SimulationProfile.GamingHeavy);
            }
        }
        else
        {
            _provider = new SimulatedSensorProvider(SimulationProfile.GamingHeavy);
        }

        _collector = new SensorCollector(_provider, TimeSpan.FromSeconds(1));
        _collector.SampleCollected += OnSampleCollected;
        _collector.CollectionError += (s, ex) => _logger.LogError(ex, "Sensor collection error occurred.");

        await _collector.StartAsync(cancellationToken);
        _logger.LogInformation("PC Sentinel Background Service is actively monitoring.");

        await base.StartAsync(cancellationToken);
    }

    private async void OnSampleCollected(object? sender, TelemetrySample sample)
    {
        try
        {
            if (_incidentRecorder == null || _classifier == null ||
                _baselineLearner == null || _correlationEngine == null ||
                _store == null || _sentinelNode == null)
            {
                return;
            }

            // 1. Continuous ring buffer ingestion
            _incidentRecorder.IngestTelemetry(sample);

            // 2. Dynamic workload classification
            var workload = _classifier.Classify(sample);

            // 3. Welford streaming baseline learning
            _baselineLearner.IngestSample(sample, workload.Workload);
            var baseline = _baselineLearner.GetBaseline(workload.Workload);

            // 4. Multi-signal root-cause correlation
            var analysis = _correlationEngine.Analyze(sample, baseline);

            // 5. Persist telemetry & score to SQLite WAL
            await _store.StoreTelemetrySampleAsync(sample);
            await _store.StoreHealthScoreAsync(analysis.Score);

            // 6. Black Box crash capture on critical anomalies
            if (analysis.DetectedEvents.Any(e => e.Severity == EventSeverity.Critical))
            {
                var crit = analysis.DetectedEvents.First(e => e.Severity == EventSeverity.Critical);
                _logger.LogWarning("CRITICAL HARDWARE ANOMALY: {Title}. Capturing Black Box snapshot...", crit.Title);
                await _incidentRecorder.RecordIncidentAsync(
                    crit.Title,
                    EventSeverity.Critical,
                    analysis.Score.OverallScore,
                    analysis.DetectedEvents);
            }

            // 7. Dispatch alerts
            if (_alertManager != null)
            {
                foreach (var ev in analysis.DetectedEvents)
                {
                    await _alertManager.PublishAlertAsync(ev);
                }
            }

            // 8. Stream JSON telemetry to ESP32-S3 Sentinel Node
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
            await _sentinelNode.SendTelemetryAsync(packet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry sample in background worker.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Background heartbeat logged every 60 seconds
            _logger.LogDebug("PC Sentinel background heartbeat: telemetry daemon alive.");
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PC Sentinel Background Service is shutting down...");

        if (_collector != null)
        {
            await _collector.StopAsync();
            _collector.Dispose();
        }

        if (_sentinelNode != null)
        {
            await _sentinelNode.DisconnectAsync(cancellationToken);
        }

        if (_store != null)
        {
            await _store.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
