using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Hardware;

/// <summary>
/// Background collector that samples hardware sensors at regular intervals
/// and aggregates them into coherent TelemetrySample snapshots.
/// </summary>
public sealed class SensorCollector : IDisposable
{
    private readonly ISensorProvider _provider;
    private readonly TimeSpan _samplingInterval;
    private CancellationTokenSource? _cts;
    private Task? _collectionLoopTask;

    public event EventHandler<TelemetrySample>? SampleCollected;
    public event EventHandler<Exception>? CollectionError;

    public bool IsRunning { get; private set; }

    public SensorCollector(ISensorProvider provider, TimeSpan? samplingInterval = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _samplingInterval = samplingInterval ?? TimeSpan.FromSeconds(1);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return;

        await _provider.InitializeAsync(cancellationToken);

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _collectionLoopTask = Task.Run(() => RunLoopAsync(_cts.Token), CancellationToken.None);
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        if (_collectionLoopTask != null)
        {
            try
            {
                await _collectionLoopTask;
            }
            catch (OperationCanceledException) { }
        }

        _provider.Close();
        IsRunning = false;
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_samplingInterval);

        while (!token.IsCancellationRequested)
        {
            try
            {
                var readings = await _provider.ReadSensorsAsync(token);
                var sample = AggregateSample(readings);
                SampleCollected?.Invoke(this, sample);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                CollectionError?.Invoke(this, ex);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(token))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public static TelemetrySample AggregateSample(IReadOnlyList<SensorReading> readings)
    {
        var sample = new TelemetrySample
        {
            Timestamp = readings.FirstOrDefault()?.Timestamp ?? DateTimeOffset.UtcNow,
            RawReadings = readings,

            // CPU Extraction
            CpuTemperatureC = readings.FirstOrDefault(r => r.Component == ComponentType.Cpu && r.SensorType == SensorType.Temperature)?.Value,
            CpuClockMhz = readings.FirstOrDefault(r => r.Component == ComponentType.Cpu && r.SensorType == SensorType.Clock)?.Value,
            CpuLoadPercent = readings.FirstOrDefault(r => r.Component == ComponentType.Cpu && r.SensorType == SensorType.Load)?.Value,
            CpuPowerWatts = readings.FirstOrDefault(r => r.Component == ComponentType.Cpu && r.SensorType == SensorType.Power)?.Value,

            // GPU Extraction
            GpuTemperatureC = readings.FirstOrDefault(r => r.Component == ComponentType.Gpu && r.SensorType == SensorType.Temperature && r.SensorName.Contains("Core", StringComparison.OrdinalIgnoreCase))?.Value
                              ?? readings.FirstOrDefault(r => r.Component == ComponentType.Gpu && r.SensorType == SensorType.Temperature)?.Value,
            GpuHotspotC = readings.FirstOrDefault(r => r.Component == ComponentType.Gpu && r.SensorName.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase) || r.SensorName.Contains("Hotspot", StringComparison.OrdinalIgnoreCase))?.Value,
            GpuClockMhz = readings.FirstOrDefault(r => r.Component == ComponentType.Gpu && r.SensorType == SensorType.Clock)?.Value,
            GpuLoadPercent = readings.FirstOrDefault(r => r.Component == ComponentType.Gpu && r.SensorType == SensorType.Load)?.Value,
            GpuPowerWatts = readings.FirstOrDefault(r => r.Component == ComponentType.Gpu && r.SensorType == SensorType.Power)?.Value,
            GpuFanPercent = readings.FirstOrDefault(r => r.Component == ComponentType.Gpu && r.SensorType == SensorType.Fan)?.Value,

            // RAM Extraction
            RamUsedGb = readings.FirstOrDefault(r => r.Component == ComponentType.Memory && r.SensorType == SensorType.Data && r.SensorName.Contains("Used", StringComparison.OrdinalIgnoreCase))?.Value,
            RamLoadPercent = readings.FirstOrDefault(r => r.Component == ComponentType.Memory && r.SensorType == SensorType.Load)?.Value
        };

        foreach (var storageReading in readings.Where(r => r.Component == ComponentType.Storage && r.SensorType == SensorType.Temperature))
        {
            sample.StorageTemperaturesC[storageReading.ComponentName] = storageReading.Value;
        }

        return sample;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _provider.Dispose();
    }
}
