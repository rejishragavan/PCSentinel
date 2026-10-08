using System.Collections.ObjectModel;
using System.Windows;
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
    private readonly IHealthAnalyzer _analyzer;
    private SensorCollector? _collector;
    private ISensorProvider? _provider;
    private WorkloadBaseline? _baseline;

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

    public ObservableCollection<HealthEvent> ActiveEvents { get; } = new();
    public ObservableCollection<ScoreDeduction> Deductions { get; } = new();

    public DashboardViewModel()
    {
        _store = new SqliteTelemetryStore();
        _analyzer = new RuleBasedHealthAnalyzer();
    }

    public async Task InitializeAsync()
    {
        await _store.InitializeDatabaseAsync();

        _baseline = new WorkloadBaseline
        {
            ProfileName = "Default Gaming",
            Workload = WorkloadType.Gaming,
            AvgCpuTempC = 65.0,
            AvgCpuClockMhz = 4700.0,
            AvgGpuTempC = 72.0,
            AvgGpuClockMhz = 1845.0
        };

        // If running on Windows with admin, attempt real hardware. Otherwise fall back to simulator.
        if (OperatingSystem.IsWindows())
        {
            try
            {
                _provider = new LibreHardwareSensorProvider();
                IsSimulating = false;
            }
            catch
            {
                _provider = new SimulatedSensorProvider(SimulationProfile.GamingHeavy);
                IsSimulating = true;
            }
        }
        else
        {
            _provider = new SimulatedSensorProvider(SimulationProfile.GamingHeavy);
            IsSimulating = true;
        }

        _collector = new SensorCollector(_provider, TimeSpan.FromSeconds(1));
        _collector.SampleCollected += OnSampleCollected;

        await _collector.StartAsync();
    }

    [RelayCommand]
    public void ToggleSimulationMode()
    {
        if (_provider is SimulatedSensorProvider sim)
        {
            // Cycle simulation profiles
            sim.SetProfile(SimulationProfile.ThermalThrottlingIncident);
        }
    }

    private void OnSampleCollected(object? sender, TelemetrySample sample)
    {
        var analysis = _analyzer.Analyze(sample, _baseline);

        // Store asynchronously in background
        _ = _store.StoreTelemetrySampleAsync(sample);
        _ = _store.StoreHealthScoreAsync(analysis.Score);

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

            HealthScore = analysis.Score.OverallScore;
            HealthColor = HealthScore >= 90 ? "#10B981" : HealthScore >= 75 ? "#F59E0B" : "#EF4444";
            HealthSummary = HealthScore >= 90 ? "System Healthy" : HealthScore >= 75 ? "Warning - Degradation" : "Critical Anomaly Detected";

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
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_collector != null)
        {
            await _collector.StopAsync();
            _collector.Dispose();
        }
        await _store.DisposeAsync();
    }
}
