namespace PCSentinel.Core.Models;

public record SmartAttribute(int Id, string Name, int CurrentValue, int WorstValue, int Threshold, long RawValue, string Status);

/// <summary>
/// Drive SMART diagnostics and historical degradation metrics (Phase 15).
/// </summary>
public record StorageDriveInfo
{
    public string DriveId { get; init; } = "NVMe-0";
    public string Model { get; init; } = "Samsung SSD 990 PRO 2TB";
    public string InterfaceType { get; init; } = "PCIe 4.0 x4 NVMe";
    public double TotalCapacityGb { get; init; } = 2000.0;
    public int HealthPercentage { get; init; } = 99;
    public double TemperatureC { get; init; } = 41.0;
    public int PowerOnHours { get; init; } = 1420;
    public double TotalBytesWrittenTb { get; init; } = 18.4;
    public int ReallocatedSectors { get; init; } = 0;
    public int PendingSectors { get; init; } = 0;
    public string DegradationRisk { get; init; } = "Minimal (Nominal Operation)";
    public IReadOnlyList<SmartAttribute> SmartAttributes { get; init; } = Array.Empty<SmartAttribute>();
}
