namespace PCSentinel.Core.Models;

public enum ComponentType
{
    Cpu,
    Gpu,
    Memory,
    Storage,
    Motherboard,
    Network,
    Unknown
}

public enum SensorType
{
    Temperature,
    Clock,
    Load,
    Power,
    Voltage,
    Fan,
    Flow,
    Level,
    Data,
    SmallData,
    Throughput,
    Frequency,
    Energy,
    Other
}

public enum EventSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum HealthEventType
{
    ThermalPerformanceAnomaly,
    ThermalThrottling,
    PowerThrottling,
    PerformanceDeviation,
    StorageDegradation,
    CoolingDegradation,
    DriverReset,
    UnexpectedShutdown,
    BootRegression
}
