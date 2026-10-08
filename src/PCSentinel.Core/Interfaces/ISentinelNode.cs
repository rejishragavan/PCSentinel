using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Hardware interface for communicating with the embedded ESP32-S3 Sentinel Node.
/// </summary>
public interface ISentinelNode : IAsyncDisposable
{
    bool IsConnected { get; }
    string PortName { get; }

    Task ConnectAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendTelemetryAsync(SentinelPacket packet, CancellationToken cancellationToken = default);
}
