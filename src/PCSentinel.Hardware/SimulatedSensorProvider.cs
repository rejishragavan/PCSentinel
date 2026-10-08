using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Hardware;

public enum SimulationProfile
{
    NormalIdle,
    GamingHeavy,
    ThermalThrottlingIncident
}

/// <summary>
/// Cross-platform simulated hardware sensor provider for testing, demoing, and CI.
/// Allows simulating realistic loads, temperatures, and throttling incidents.
/// </summary>
public sealed class SimulatedSensorProvider : ISensorProvider
{
    private readonly Random _rand = new();
    private SimulationProfile _profile;
    private double _tickCount;

    public string ProviderName => "SimulatedHardware";
    public bool IsSupported => true;

    public SimulatedSensorProvider(SimulationProfile profile = SimulationProfile.NormalIdle)
    {
        _profile = profile;
    }

    public void SetProfile(SimulationProfile profile) => _profile = profile;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<SensorReading>> ReadSensorsAsync(CancellationToken cancellationToken = default)
    {
        _tickCount += 0.2;
        var now = DateTimeOffset.UtcNow;
        var readings = new List<SensorReading>();

        double cpuTemp, cpuClock, cpuLoad, cpuPower;
        double gpuTemp, gpuHotspot, gpuClock, gpuLoad, gpuPower, gpuFan;
        double ramUsedGb = 9.4 + Math.Sin(_tickCount) * 0.4;
        const double ramTotalGb = 32.0;

        switch (_profile)
        {
            case SimulationProfile.GamingHeavy:
                cpuTemp = 68.0 + Math.Sin(_tickCount) * 4.0 + _rand.NextDouble();
                cpuClock = 4750.0 + _rand.NextDouble() * 50;
                cpuLoad = 62.0 + Math.Sin(_tickCount * 0.5) * 15;
                cpuPower = 78.0 + _rand.NextDouble() * 5;

                gpuTemp = 72.0 + Math.Sin(_tickCount * 0.8) * 3.0;
                gpuHotspot = gpuTemp + 11.5 + _rand.NextDouble();
                gpuClock = 1845.0 + _rand.NextDouble() * 20;
                gpuLoad = 96.0 + _rand.NextDouble() * 4;
                gpuPower = 215.0 + _rand.NextDouble() * 10;
                gpuFan = 65.0 + _rand.NextDouble() * 3;
                break;

            case SimulationProfile.ThermalThrottlingIncident:
                // Simulating deteriorating thermals with downclocking
                cpuTemp = 88.0 + Math.Sin(_tickCount) * 3.0;
                cpuClock = 4100.0 - _rand.NextDouble() * 200;
                cpuLoad = 95.0;
                cpuPower = 110.0;

                gpuTemp = 86.5 + _rand.NextDouble() * 2; // > 83 limit
                gpuHotspot = 104.0 + _rand.NextDouble() * 2;
                gpuClock = 1420.0 - _rand.NextDouble() * 60; // downclocked from 1845 MHz!
                gpuLoad = 99.0;
                gpuPower = 175.0; // power capped
                gpuFan = 100.0;   // fan pinned at 100%
                break;

            case SimulationProfile.NormalIdle:
            default:
                cpuTemp = 41.0 + Math.Sin(_tickCount) * 2.0;
                cpuClock = 2800.0 + _rand.NextDouble() * 300;
                cpuLoad = 8.0 + _rand.NextDouble() * 5;
                cpuPower = 24.0 + _rand.NextDouble() * 3;

                gpuTemp = 43.0 + Math.Sin(_tickCount) * 1.5;
                gpuHotspot = gpuTemp + 6.0;
                gpuClock = 450.0 + _rand.NextDouble() * 50;
                gpuLoad = 3.0 + _rand.NextDouble() * 2;
                gpuPower = 18.0 + _rand.NextDouble() * 2;
                gpuFan = 0.0; // 0 RPM idle mode
                break;
        }

        // CPU
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Cpu, ComponentName = "Intel Core i7-14700K", SensorType = SensorType.Temperature, SensorName = "CPU Package", Value = cpuTemp, Unit = "°C", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Cpu, ComponentName = "Intel Core i7-14700K", SensorType = SensorType.Clock, SensorName = "CPU Core #1 Clock", Value = cpuClock, Unit = "MHz", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Cpu, ComponentName = "Intel Core i7-14700K", SensorType = SensorType.Load, SensorName = "CPU Total Load", Value = cpuLoad, Unit = "%", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Cpu, ComponentName = "Intel Core i7-14700K", SensorType = SensorType.Power, SensorName = "CPU Package Power", Value = cpuPower, Unit = "W", Source = ProviderName });

        // GPU
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Gpu, ComponentName = "NVIDIA GeForce RTX 4070 Ti", SensorType = SensorType.Temperature, SensorName = "GPU Core", Value = gpuTemp, Unit = "°C", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Gpu, ComponentName = "NVIDIA GeForce RTX 4070 Ti", SensorType = SensorType.Temperature, SensorName = "GPU Hotspot", Value = gpuHotspot, Unit = "°C", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Gpu, ComponentName = "NVIDIA GeForce RTX 4070 Ti", SensorType = SensorType.Clock, SensorName = "GPU Core Clock", Value = gpuClock, Unit = "MHz", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Gpu, ComponentName = "NVIDIA GeForce RTX 4070 Ti", SensorType = SensorType.Load, SensorName = "GPU Core Load", Value = gpuLoad, Unit = "%", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Gpu, ComponentName = "NVIDIA GeForce RTX 4070 Ti", SensorType = SensorType.Power, SensorName = "GPU Power", Value = gpuPower, Unit = "W", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Gpu, ComponentName = "NVIDIA GeForce RTX 4070 Ti", SensorType = SensorType.Fan, SensorName = "GPU Fan Speed", Value = gpuFan, Unit = "%", Source = ProviderName });

        // Memory
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Memory, ComponentName = "DDR5 Memory", SensorType = SensorType.Data, SensorName = "Memory Used", Value = ramUsedGb, Unit = "GB", Source = ProviderName });
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Memory, ComponentName = "DDR5 Memory", SensorType = SensorType.Load, SensorName = "Memory Load", Value = (ramUsedGb / ramTotalGb) * 100.0, Unit = "%", Source = ProviderName });

        // Storage
        readings.Add(new SensorReading { Timestamp = now, Component = ComponentType.Storage, ComponentName = "Samsung 990 PRO 2TB", SensorType = SensorType.Temperature, SensorName = "Drive Temperature", Value = 44.0 + Math.Sin(_tickCount * 0.3), Unit = "°C", Source = ProviderName });

        return Task.FromResult<IReadOnlyList<SensorReading>>(readings);
    }

    public void Close() { }
    public void Dispose() { }
}
