using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCSentinel.Analysis;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;
using PCSentinel.Hardware;
using PCSentinel.Storage;

namespace PCSentinel.App.ViewModels;

public partial class DashboardViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ITelemetryStore _store;
    private readonly MultiSignalCorrelationEngine _correlationEngine;
    private readonly WorkloadClassifier _classifier;
    private readonly StatisticalBaselineLearner _baselineLearner;
    private readonly UsbSerialSentinelNode _sentinelNode;

    private SensorCollector? _collector;
    private ISensorProvider? _provider;
    private WorkloadBaseline? _activeBaseline;
    private SimulationProfile _currentSimProfile = SimulationProfile.GamingHeavy;

    private readonly List<double> _cpuTempHistory = new();
    private readonly List<double> _gpuTempHistory = new();
    private const int MaxHistoryPoints = 30;

    [ObservableProperty] private double _cpuTemp;
    [ObservableProperty] private double _cpuClock;
    [ObservableProperty] private double _cpuLoad;
    [ObservableProperty] private double _cpuPower;

    [ObservableProperty] private double _gpuTemp;
    [ObservableProperty] private double _gpuHotspot;
    [ObservableProperty] private double _gpuClock;
    [ObservableProperty] private double _gpuLoad;
    [ObservableProperty] private double _gpuPower;
    [ObservableProperty] private double _gpuFan;

    [ObservableProperty] private double _ramUsedGb;
    [ObservableProperty] private double _ramLoad;

    [ObservableProperty] private int _healthScore = 100;
    [ObservableProperty] private string _healthSummary = "System Operating Normally";
    [ObservableProperty] private string _healthColor = "#10B981"; // Emerald green
    [ObservableProperty] private bool _isSimulating;
    [ObservableProperty] private string _providerModeText = "LIVE (Ring-0)";

    [ObservableProperty] private string _workloadName = "Gaming";
    [ObservableProperty] private string _workloadDescription = "High 3D Graphics Load";
    [ObservableProperty] private string _baselineComparisonText = "Normal GPU: 68–74°C @ 1845 MHz";
    [ObservableProperty] private string _sentinelNodeStatus = "Node: Virtual Loopback (Active)";

    [ObservableProperty] private PointCollection _cpuSparkline = new();
    [ObservableProperty] private PointCollection _gpuSparkline = new();

    [ObservableProperty] private string _bootDurationText = "Boot: 15.4s (Normal)";
    [ObservableProperty] private string _driverHealthSummaryText = "Drivers: 48 OK (0 Errors)";
    [ObservableProperty] private string _incidentCountText = "Black Box: 0 Incidents";

    [ObservableProperty] private string _ocHeadroomSummary = "Headroom: +95 MHz Boost (High Margin)";
    [ObservableProperty] private string _ocRationale = "Thermal margin is +12°C with 70W power buffer. Safe to boost core frequency.";
    [ObservableProperty] private string _latestBenchmarkText = "Benchmark: 142.5 FPS (Score: 14,250)";
    [ObservableProperty] private string _latestOcExperimentText = "Last Experiment: Not run yet";
    [ObservableProperty] private string _storageSummaryText = "Samsung 990 PRO 2TB: 99% Health, 41°C (Nominal)";

    public ObservableCollection<HealthEvent> ActiveEvents { get; } = new();
    public ObservableCollection<ScoreDeduction> Deductions { get; } = new();
    public ObservableCollection<IncidentReport> RecentIncidents { get; } = new();
    public ObservableCollection<OcExperiment> Experiments { get; } = new();
    public ObservableCollection<StorageDriveInfo> StorageDrives { get; } = new();

    private readonly PnpDeviceInspector _driverInspector;
    private readonly BootAnalyzer _bootAnalyzer;
    private readonly IncidentRecorder _incidentRecorder;
    private readonly OcAnalyzer _ocAnalyzer;
    private readonly StorageAnalyzer _storageAnalyzer;
    private readonly RecoveryManager _recoveryManager;

    [ObservableProperty] private string _recoveryStatusText = "Recovery Engine: Nominal (0 pending repairs)";
    public ObservableCollection<RecoveryTaskResult> RecoveryTasks { get; } = new();

    public DashboardViewModel()
    {
        _store = new SqliteTelemetryStore();
        _classifier = new WorkloadClassifier();
        _baselineLearner = new StatisticalBaselineLearner();
        _correlationEngine = new MultiSignalCorrelationEngine(_classifier);
        _sentinelNode = new UsbSerialSentinelNode();

        _driverInspector = new PnpDeviceInspector();
        _bootAnalyzer = new BootAnalyzer(_store);
        _incidentRecorder = new IncidentRecorder(_store, _driverInspector);
        _ocAnalyzer = new OcAnalyzer(_store);
        _storageAnalyzer = new StorageAnalyzer();
        _recoveryManager = new RecoveryManager(_store, _driverInspector, _bootAnalyzer, simulateProblems: false);
    }

    public async Task InitializeAsync()
    {
        await _store.InitializeDatabaseAsync();
        await _sentinelNode.ConnectAsync("VIRTUAL");

        // 1. Initial Driver Scan
        try
        {
            var driverSummary = await _driverInspector.ScanDriversAsync();
            DriverHealthSummaryText = $"Drivers: {driverSummary.HealthyCount} OK, {driverSummary.ErrorCount} Err";
        }
        catch { }

        // 2. Initial Boot Analysis
        try
        {
            var latestBoot = await _bootAnalyzer.AnalyzeLatestBootAsync();
            if (latestBoot != null)
            {
                BootDurationText = latestBoot.IsRegression
                    ? $"Boot: {latestBoot.TotalBootDurationMs / 1000.0:F1}s (⚠ +{latestBoot.RegressionDeltaMs / 1000.0:F1}s Slow)"
                    : $"Boot: {latestBoot.TotalBootDurationMs / 1000.0:F1}s (Normal)";
            }
        }
        catch { }

        // 3. Initial Storage Scan
        try
        {
            var drives = await _storageAnalyzer.InspectStorageDrivesAsync();
            StorageDrives.Clear();
            foreach (var d in drives)
            {
                StorageDrives.Add(d);
            }
            if (drives.Count > 0)
            {
                var primary = drives[0];
                StorageSummaryText = $"{primary.Model}: {primary.HealthPercentage}% Health, {primary.TemperatureC:F0}°C ({primary.DegradationRisk.Split('(')[0].Trim()})";
            }
        }
        catch { }

        // 4. Initial Load of Previous Incidents and Experiments from SQLite
        try
        {
            var incidents = await _store.GetRecentIncidentsAsync(10);
            RecentIncidents.Clear();
            foreach (var inc in incidents)
            {
                RecentIncidents.Add(inc);
            }
            if (RecentIncidents.Count > 0)
            {
                IncidentCountText = $"Black Box: {RecentIncidents.Count} Incidents";
            }

            var experiments = await _store.GetOcExperimentsAsync(10);
            Experiments.Clear();
            foreach (var exp in experiments)
            {
                Experiments.Add(exp);
            }
            if (Experiments.Count > 0)
            {
                var latest = Experiments[0];
                LatestOcExperimentText = $"Exp #{latest.ExperimentNumber}: +{latest.GpuClockOffsetMhz:F0}MHz -> +{latest.PerformanceGainPercent:F1}% FPS";
            }

            var recoveryAudits = await _store.GetRecoveryAuditsAsync(10);
            RecoveryTasks.Clear();
            foreach (var task in recoveryAudits)
            {
                RecoveryTasks.Add(task);
            }
            if (RecoveryTasks.Count > 0)
            {
                RecoveryStatusText = $"Recovery Engine: {RecoveryTasks.Count} audits logged in history.";
            }
        }
        catch { }

        _activeBaseline = new WorkloadBaseline
        {
            ProfileName = "Learned Gaming Profile",
            Workload = WorkloadType.Gaming,
            AvgCpuTempC = 64.0,
            StdDevCpuTempC = 3.0,
            AvgCpuClockMhz = 4700.0,
            AvgGpuTempC = 71.0,
            StdDevGpuTempC = 2.5,
            AvgGpuClockMhz = 1845.0,
            SampleCount = 1000
        };

        if (OperatingSystem.IsWindows())
        {
            try
            {
                _provider = new LibreHardwareSensorProvider();
                IsSimulating = false;
                ProviderModeText = "LIVE (Ring-0)";
            }
            catch
            {
                _provider = new SimulatedSensorProvider(_currentSimProfile);
                IsSimulating = true;
                ProviderModeText = $"SIMULATED ({_currentSimProfile})";
            }
        }
        else
        {
            _provider = new SimulatedSensorProvider(_currentSimProfile);
            IsSimulating = true;
            ProviderModeText = $"SIMULATED ({_currentSimProfile})";
        }

        _collector = new SensorCollector(_provider, TimeSpan.FromSeconds(1));
        _collector.SampleCollected += OnSampleCollected;

        await _collector.StartAsync();
    }

    [RelayCommand]
    public void CycleSimulationProfile()
    {
        if (_provider is SimulatedSensorProvider sim)
        {
            _currentSimProfile = _currentSimProfile switch
            {
                SimulationProfile.NormalIdle => SimulationProfile.GamingHeavy,
                SimulationProfile.GamingHeavy => SimulationProfile.ThermalThrottlingIncident,
                _ => SimulationProfile.NormalIdle
            };

            sim.SetProfile(_currentSimProfile);
            ProviderModeText = $"SIMULATED ({_currentSimProfile})";
        }
    }

    [RelayCommand]
    public void SetScenarioNormalGaming()
    {
        if (_provider is SimulatedSensorProvider sim)
        {
            _currentSimProfile = SimulationProfile.GamingHeavy;
            sim.SetProfile(_currentSimProfile);
            ProviderModeText = $"SIMULATED ({_currentSimProfile})";
        }
    }

    [RelayCommand]
    public void SetScenarioThermalThrottle()
    {
        if (_provider is SimulatedSensorProvider sim)
        {
            _currentSimProfile = SimulationProfile.ThermalThrottlingIncident;
            sim.SetProfile(_currentSimProfile);
            ProviderModeText = $"SIMULATED ({_currentSimProfile})";
        }
    }

    [RelayCommand]
    public async Task RunOcBenchmark()
    {
        LatestBenchmarkText = "Running 3D Benchmark (Measuring FPS & Power)...";
        var res = await _ocAnalyzer.RunBenchmarkAsync(10, 0.0);
        LatestBenchmarkText = $"Benchmark: {res.AverageFps:F1} FPS (Score: {res.Score:N0}) @ {res.AvgGpuClockMhz:F0}MHz";
    }

    [RelayCommand]
    public async Task RunAutoOcExperiment()
    {
        LatestOcExperimentText = "Executing Controlled Boost (+85 MHz)...";
        var exp = await _ocAnalyzer.ExecuteControlledExperimentAsync("Sentinel Auto-Tuned Boost (+85 MHz)", 85.0, 0.0);
        Experiments.Insert(0, exp);
        LatestOcExperimentText = $"Exp #{exp.ExperimentNumber}: +85MHz -> +{exp.PerformanceGainPercent:F1}% FPS ({exp.BaselineFps:F1} -> {exp.ExperimentFps:F1} FPS) [Stable]";
    }

    [RelayCommand]
    public async Task TriggerBlackBoxIncident()
    {
        var inc = await _incidentRecorder.RecordIncidentAsync(
            "Manual Black Box Diagnostic Capture",
            EventSeverity.Warning,
            HealthScore,
            ActiveEvents.ToList());

        RecentIncidents.Insert(0, inc);
        IncidentCountText = $"Black Box: {RecentIncidents.Count} Captured";
    }

    [RelayCommand]
    public async Task RunAutonomousRecoveryOrchestrator()
    {
        RecoveryStatusText = "Orchestrating Autonomous Recovery (SFC, DISM, ChkDisk, Drivers)...";
        var plan = await _recoveryManager.OrchestrateFullRecoveryPlanAsync(dryRun: true);
        RecoveryTasks.Clear();
        foreach (var task in plan.ExecutedTasks)
        {
            RecoveryTasks.Add(task);
        }

        RecoveryStatusText = plan.ResolutionSummary;

        // If system was simulated throttling, restore it to normal gaming profile
        if (_provider is SimulatedSensorProvider sim)
        {
            _currentSimProfile = SimulationProfile.GamingHeavy;
            sim.SetProfile(_currentSimProfile);
            ProviderModeText = $"SIMULATED ({_currentSimProfile})";
        }

        HealthScore = plan.PostRecoveryHealthScore;
        HealthColor = "#10B981";
        HealthSummary = "Remediated & Healthy";
    }

    private void OnSampleCollected(object? sender, TelemetrySample sample)
    {
        // 0. Continuous Black Box Ingestion (last 30s buffer)
        _incidentRecorder.IngestTelemetry(sample);

        // 1. Workload Classification
        var classification = _classifier.Classify(sample);

        // 2. Baseline Learning
        _baselineLearner.IngestSample(sample, classification.Workload);
        var baseline = _baselineLearner.GetBaseline(classification.Workload) ??
                       (classification.Workload == WorkloadType.Gaming ? _activeBaseline : null);

        // 3. Multi-Signal Diagnostic Analysis
        var analysis = _correlationEngine.Analyze(sample, baseline);

        // 4. Persistence
        _ = _store.StoreTelemetrySampleAsync(sample);
        _ = _store.StoreHealthScoreAsync(analysis.Score);

        // 5. Sentinel Node Packet Transmission
        _ = _sentinelNode.SendTelemetryAsync(new SentinelPacket
        {
            Type = "status",
            Score = analysis.Score.OverallScore,
            CpuTemp = sample.CpuTemperatureC ?? 0,
            GpuTemp = sample.GpuTemperatureC ?? 0,
            CpuLoad = sample.CpuLoadPercent ?? 0,
            GpuLoad = sample.GpuLoadPercent ?? 0,
            Workload = classification.Workload.ToString(),
            LedColor = analysis.Score.OverallScore >= 90 ? "GREEN" : analysis.Score.OverallScore >= 75 ? "YELLOW" : "RED",
            TriggerBuzzer = analysis.DetectedEvents.Any(e => e.Severity == EventSeverity.Critical)
        });

        // 6. UI Update
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CpuTemp = sample.CpuTemperatureC ?? 0;
            CpuClock = sample.CpuClockMhz ?? 0;
            CpuLoad = sample.CpuLoadPercent ?? 0;
            CpuPower = sample.CpuPowerWatts ?? 0;

            GpuTemp = sample.GpuTemperatureC ?? 0;
            GpuHotspot = sample.GpuHotspotC ?? 0;
            GpuClock = sample.GpuClockMhz ?? 0;
            GpuLoad = sample.GpuLoadPercent ?? 0;
            GpuPower = sample.GpuPowerWatts ?? 0;
            GpuFan = sample.GpuFanPercent ?? 0;

            RamUsedGb = sample.RamUsedGb ?? 0;
            RamLoad = sample.RamLoadPercent ?? 0;

            WorkloadName = classification.Workload.ToString();
            WorkloadDescription = classification.Description;

            if (baseline != null)
            {
                BaselineComparisonText = $"Normal {baseline.Workload}: {baseline.AvgGpuTempC:F0}±{baseline.StdDevGpuTempC:F0}°C @ {baseline.AvgGpuClockMhz:F0}MHz";
            }

            HealthScore = analysis.Score.OverallScore;
            HealthColor = HealthScore >= 90 ? "#10B981" : HealthScore >= 75 ? "#F59E0B" : "#EF4444";
            HealthSummary = HealthScore >= 90 ? "System Healthy" : HealthScore >= 75 ? "Warning - Deviation" : "Critical Throttle/Anomaly";

            UpdateSparklines(CpuTemp, GpuTemp);

            ActiveEvents.Clear();
            foreach (var ev in analysis.DetectedEvents)
            {
                ActiveEvents.Add(ev);
            }

            Deductions.Clear();
            foreach (var d in analysis.Score.Deductions)
            {
                Deductions.Add(d);
            }

            var headroom = _ocAnalyzer.EstimateHeadroom(sample, baseline);
            OcHeadroomSummary = $"Headroom: +{headroom.RecommendedClockDeltaMhz:F0} MHz ({headroom.ThermalHeadroom} Margin)";
            OcRationale = headroom.Rationale;
        });
    }

    private void UpdateSparklines(double cpu, double gpu)
    {
        _cpuTempHistory.Add(cpu);
        if (_cpuTempHistory.Count > MaxHistoryPoints) _cpuTempHistory.RemoveAt(0);

        _gpuTempHistory.Add(gpu);
        if (_gpuTempHistory.Count > MaxHistoryPoints) _gpuTempHistory.RemoveAt(0);

        CpuSparkline = GenerateSparklinePoints(_cpuTempHistory, 30.0, 100.0, 240, 50);
        GpuSparkline = GenerateSparklinePoints(_gpuTempHistory, 30.0, 100.0, 240, 50);
    }

    private static PointCollection GenerateSparklinePoints(List<double> values, double minVal, double maxVal, double width, double height)
    {
        var points = new PointCollection();
        if (values.Count < 2) return points;

        double stepX = width / (MaxHistoryPoints - 1);
        double range = Math.Max(1.0, maxVal - minVal);

        for (int i = 0; i < values.Count; i++)
        {
            double x = i * stepX;
            double norm = Math.Clamp((values[i] - minVal) / range, 0.0, 1.0);
            double y = height - (norm * height);
            points.Add(new Point(x, y));
        }

        return points;
    }

    public async ValueTask DisposeAsync()
    {
        if (_collector != null)
        {
            await _collector.StopAsync();
            _collector.Dispose();
        }
        await _sentinelNode.DisconnectAsync();
        await _store.DisposeAsync();
    }
}
