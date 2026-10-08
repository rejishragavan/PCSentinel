using LibreHardwareMonitor.Hardware;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;
using CoreComponentType = PCSentinel.Core.Models.ComponentType;
using CoreSensorType = PCSentinel.Core.Models.SensorType;

namespace PCSentinel.Hardware;

/// <summary>
/// Production sensor provider powered by LibreHardwareMonitorLib.
/// Wraps ring-0 kernel driver readings and normalizes them into PCSentinel domain models.
/// </summary>
public sealed class LibreHardwareSensorProvider : ISensorProvider
{
    private Computer? _computer;
    private bool _initialized;

    public string ProviderName => "LibreHardwareMonitor";
    public bool IsSupported => OperatingSystem.IsWindows();

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException(
                "LibreHardwareSensorProvider requires a Windows operating system with administrative privileges.");
        }

        if (_initialized) return Task.CompletedTask;

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsNetworkEnabled = true
        };

        _computer.Open();
        _initialized = true;

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SensorReading>> ReadSensorsAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized || _computer == null)
        {
            throw new InvalidOperationException("LibreHardwareSensorProvider must be initialized before reading.");
        }

        var results = new List<SensorReading>();
        var now = DateTimeOffset.UtcNow;

        foreach (var hardware in _computer.Hardware)
        {
            UpdateAndCollect(hardware, results, now);
        }

        return Task.FromResult<IReadOnlyList<SensorReading>>(results);
    }

    private void UpdateAndCollect(IHardware hardware, List<SensorReading> list, DateTimeOffset timestamp)
    {
        hardware.Update();

        var compType = MapHardwareType(hardware.HardwareType);

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value.HasValue)
            {
                list.Add(new SensorReading
                {
                    Timestamp = timestamp,
                    Component = compType,
                    ComponentName = hardware.Name,
                    SensorType = MapSensorType(sensor.SensorType),
                    SensorName = sensor.Name,
                    Value = sensor.Value.Value,
                    Unit = GetUnit(sensor.SensorType),
                    IsAvailable = true,
                    Source = "LibreHardwareMonitor"
                });
            }
        }

        // Subhardware (e.g. secondary GPUs, nested drive controllers)
        foreach (var subHardware in hardware.SubHardware)
        {
            UpdateAndCollect(subHardware, list, timestamp);
        }
    }

    public void Close()
    {
        if (_computer != null)
        {
            _computer.Close();
            _computer = null;
        }
        _initialized = false;
    }

    public void Dispose()
    {
        Close();
    }

    private static CoreComponentType MapHardwareType(HardwareType type) => type switch
    {
        HardwareType.Cpu => CoreComponentType.Cpu,
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => CoreComponentType.Gpu,
        HardwareType.Memory => CoreComponentType.Memory,
        HardwareType.Storage => CoreComponentType.Storage,
        HardwareType.Motherboard or HardwareType.SuperIO => CoreComponentType.Motherboard,
        HardwareType.Network => CoreComponentType.Network,
        _ => CoreComponentType.Unknown
    };

    private static CoreSensorType MapSensorType(LibreHardwareMonitor.Hardware.SensorType type) => type switch
    {
        LibreHardwareMonitor.Hardware.SensorType.Temperature => CoreSensorType.Temperature,
        LibreHardwareMonitor.Hardware.SensorType.Clock => CoreSensorType.Clock,
        LibreHardwareMonitor.Hardware.SensorType.Load => CoreSensorType.Load,
        LibreHardwareMonitor.Hardware.SensorType.Power => CoreSensorType.Power,
        LibreHardwareMonitor.Hardware.SensorType.Voltage => CoreSensorType.Voltage,
        LibreHardwareMonitor.Hardware.SensorType.Fan => CoreSensorType.Fan,
        LibreHardwareMonitor.Hardware.SensorType.Flow => CoreSensorType.Flow,
        LibreHardwareMonitor.Hardware.SensorType.Level => CoreSensorType.Level,
        LibreHardwareMonitor.Hardware.SensorType.Data => CoreSensorType.Data,
        LibreHardwareMonitor.Hardware.SensorType.SmallData => CoreSensorType.SmallData,
        LibreHardwareMonitor.Hardware.SensorType.Throughput => CoreSensorType.Throughput,
        LibreHardwareMonitor.Hardware.SensorType.Energy => CoreSensorType.Energy,
        _ => CoreSensorType.Other
    };

    private static string GetUnit(LibreHardwareMonitor.Hardware.SensorType type) => type switch
    {
        LibreHardwareMonitor.Hardware.SensorType.Temperature => "°C",
        LibreHardwareMonitor.Hardware.SensorType.Clock => "MHz",
        LibreHardwareMonitor.Hardware.SensorType.Load => "%",
        LibreHardwareMonitor.Hardware.SensorType.Power => "W",
        LibreHardwareMonitor.Hardware.SensorType.Voltage => "V",
        LibreHardwareMonitor.Hardware.SensorType.Fan => "RPM",
        LibreHardwareMonitor.Hardware.SensorType.Level => "%",
        LibreHardwareMonitor.Hardware.SensorType.Data => "GB",
        LibreHardwareMonitor.Hardware.SensorType.Throughput => "B/s",
        _ => string.Empty
    };
}
