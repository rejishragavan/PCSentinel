using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using PCSentinel.Core.Models;

namespace PCSentinel.Diagnostics;

public record SystemLogIncident(DateTimeOffset Timestamp, string ProviderName, int EventId, string Description, EventSeverity Severity);

/// <summary>
/// Inspects Windows Event Logs for critical crash/hang signals:
/// - Event ID 4101 (nvlddmkm): Display Driver TDR (Timeout Detection and Recovery)
/// - Event ID 41 (Kernel-Power): Unclean shutdown / BSOD reboot
/// - Event ID 18/19 (WHEA-Logger): Hardware Machine Check Exception (CPU/PCIe bus error)
/// </summary>
public sealed class WindowsEventLogInspector
{
    private readonly bool _simulateTdrIncident;

    public WindowsEventLogInspector(bool simulateTdrIncident = false)
    {
        _simulateTdrIncident = simulateTdrIncident;
    }

    public Task<IReadOnlyList<SystemLogIncident>> QueryRecentCriticalEventsAsync(int maxHours = 24, CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsWindows() && !_simulateTdrIncident)
        {
            try
            {
                return Task.FromResult<IReadOnlyList<SystemLogIncident>>(ReadWindowsEventLogs(maxHours));
            }
            catch
            {
                return Task.FromResult<IReadOnlyList<SystemLogIncident>>(GenerateSimulatedEvents(_simulateTdrIncident));
            }
        }

        return Task.FromResult<IReadOnlyList<SystemLogIncident>>(GenerateSimulatedEvents(_simulateTdrIncident));
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<SystemLogIncident> ReadWindowsEventLogs(int maxHours)
    {
        var results = new List<SystemLogIncident>();
        var startTime = DateTime.UtcNow.AddHours(-maxHours);

        // Query System log for Kernel-Power (41) and WHEA-Logger (18, 19) and Display (4101)
        var query = $"*[System[(EventID=41 or EventID=4101 or EventID=18 or EventID=19) and TimeCreated[timediff(@SystemTime) <= {maxHours * 3600000}]]]";
        var eventQuery = new EventLogQuery("System", PathType.LogName, query);

        using var reader = new EventLogReader(eventQuery);
        EventRecord? record;

        while ((record = reader.ReadEvent()) != null)
        {
            using (record)
            {
                var id = record.Id;
                var time = record.TimeCreated ?? DateTime.UtcNow;
                var provider = record.ProviderName;
                var desc = record.FormatDescription() ?? $"Event {id} reported by {provider}";

                var severity = id switch
                {
                    41 => EventSeverity.Critical,
                    4101 => EventSeverity.Warning,
                    18 or 19 => EventSeverity.Critical,
                    _ => EventSeverity.Info
                };

                results.Add(new SystemLogIncident(new DateTimeOffset(time), provider, id, desc, severity));
            }
        }

        return results;
    }

    private static IReadOnlyList<SystemLogIncident> GenerateSimulatedEvents(bool injectTdr)
    {
        var list = new List<SystemLogIncident>();
        var now = DateTimeOffset.UtcNow;

        if (injectTdr)
        {
            list.Add(new SystemLogIncident(
                now.AddMinutes(-5),
                "nvlddmkm",
                4101,
                "Display driver nvlddmkm stopped responding and has successfully recovered (GPU TDR incident).",
                EventSeverity.Warning));
        }

        return list;
    }
}
