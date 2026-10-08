using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Abstraction for polling sensor readings from hardware or simulator.
/// </summary>
public interface ISensorProvider : IDisposable
{
    string ProviderName { get; }
    bool IsSupported { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SensorReading>> ReadSensorsAsync(CancellationToken cancellationToken = default);
    void Close();
}
