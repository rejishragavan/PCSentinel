using System.Text.Json.Serialization;

namespace PCSentinel.Core.Models;

/// <summary>
/// Compact wire packet transmitted across USB Serial to the ESP32-S3 embedded Sentinel Node.
/// Powers the 1.3" OLED display, RGB status LED (Green/Yellow/Red), and piezo buzzer.
/// </summary>
public record SentinelPacket
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "status"; // "status" or "alert"

    [JsonPropertyName("score")]
    public int Score { get; init; } = 100;

    [JsonPropertyName("cpu_t")]
    public double CpuTemp { get; init; }

    [JsonPropertyName("gpu_t")]
    public double GpuTemp { get; init; }

    [JsonPropertyName("cpu_l")]
    public double CpuLoad { get; init; }

    [JsonPropertyName("gpu_l")]
    public double GpuLoad { get; init; }

    [JsonPropertyName("workload")]
    public string Workload { get; init; } = "Idle";

    [JsonPropertyName("led")]
    public string LedColor { get; init; } = "GREEN"; // GREEN, YELLOW, RED

    [JsonPropertyName("alert_title")]
    public string? AlertTitle { get; init; }

    [JsonPropertyName("beep")]
    public bool TriggerBuzzer { get; init; }

    [JsonPropertyName("culprit")]
    public string? CulpritProcess { get; init; }

    [JsonPropertyName("culprit_cpu")]
    public double CulpritCpuPercent { get; init; }

    [JsonPropertyName("oc_mode")]
    public string? ActiveOcMode { get; init; }
}

/// <summary>
/// Upstream command sent from ESP32-S3 hardware node to the PC Sentinel host over USB Serial.
/// </summary>
public record SentinelCommandPacket
{
    [JsonPropertyName("cmd")]
    public string Command { get; init; } = string.Empty; // "overclock", "benchmark", "clear_alert"

    [JsonPropertyName("target")]
    public string Target { get; init; } = "gpu"; // "gpu", "cpu", "ram", "all"
}

/// <summary>
/// Downstream acknowledgement packet sent back to ESP32 node confirming applied tuning.
/// </summary>
public record SentinelOcAckPacket
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "oc_ack";

    [JsonPropertyName("target")]
    public string Target { get; init; } = "GPU";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "STABLE";

    [JsonPropertyName("gain")]
    public double GainPercent { get; init; }

    [JsonPropertyName("msg")]
    public string Message { get; init; } = string.Empty;
}

