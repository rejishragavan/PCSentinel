namespace PCSentinel.Core.Models;

public enum DeviceStatusCode
{
    Ok,
    Degraded,
    Error,
    Disabled,
    Unknown
}

public record DriverInfo
{
    public string DeviceId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string Category { get; init; } = "Other"; // GPU, Network, Audio, Storage, System
    public DeviceStatusCode Status { get; init; } = DeviceStatusCode.Ok;
    public string DriverVersion { get; init; } = "Unknown";
    public string Manufacturer { get; init; } = string.Empty;
    public int? ConfigManagerErrorCode { get; init; } // e.g., Code 43, Code 10
    public string? ErrorDescription { get; init; }
    public DateTimeOffset LastChecked { get; init; } = DateTimeOffset.UtcNow;
}

public record DriverHealthSummary
{
    public int TotalDevices { get; init; }
    public int HealthyCount { get; init; }
    public int DegradedCount { get; init; }
    public int ErrorCount { get; init; }
    public IReadOnlyList<DriverInfo> Devices { get; init; } = Array.Empty<DriverInfo>();
    public bool HasCriticalFailure => ErrorCount > 0;
}
