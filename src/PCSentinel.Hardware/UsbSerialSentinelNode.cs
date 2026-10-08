using System.IO.Ports;
using System.Text.Json;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Hardware;

/// <summary>
/// Driver for transmitting telemetry and alert packets to the ESP32-S3 Sentinel Node
/// over USB Serial (or virtual loopback during firmware development).
/// </summary>
public sealed class UsbSerialSentinelNode : ISentinelNode
{
    private SerialPort? _serialPort;
    private bool _isVirtual;

    public bool IsConnected => _isVirtual || (_serialPort != null && _serialPort.IsOpen);
    public string PortName { get; private set; } = "None";

    public event EventHandler<string>? PacketTransmitted;

    public Task ConnectAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default)
    {
        PortName = portName;

        if (string.Equals(portName, "VIRTUAL", StringComparison.OrdinalIgnoreCase))
        {
            _isVirtual = true;
            return Task.CompletedTask;
        }

        try
        {
            _serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                NewLine = "\n"
            };
            _serialPort.Open();
            _isVirtual = false;
        }
        catch
        {
            // Fall back to virtual mode so app continues without crashing
            _isVirtual = true;
            PortName = $"{portName} (Virtual Fallback)";
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
            _serialPort.Dispose();
            _serialPort = null;
        }
        _isVirtual = false;
        PortName = "Disconnected";
        return Task.CompletedTask;
    }

    public async Task SendTelemetryAsync(SentinelPacket packet, CancellationToken cancellationToken = default)
    {
        if (!IsConnected) return;

        var json = JsonSerializer.Serialize(packet);

        if (_serialPort != null && _serialPort.IsOpen)
        {
            await Task.Run(() => _serialPort.WriteLine(json), cancellationToken);
        }

        PacketTransmitted?.Invoke(this, json);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
