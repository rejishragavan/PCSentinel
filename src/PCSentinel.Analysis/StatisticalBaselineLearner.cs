using System.Collections.Concurrent;
using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Analysis;

/// <summary>
/// Implements Welford's algorithm to compute accurate streaming mean and variance
/// across incoming telemetry samples without maintaining high-cardinality data in memory.
/// </summary>
public sealed class StatisticalBaselineLearner : IBaselineLearner
{
    private class WelfordAccumulator
    {
        public int Count { get; private set; }
        public double Mean { get; private set; }
        private double _m2;

        public double Variance => Count > 1 ? _m2 / (Count - 1) : 0.0;
        public double StdDev => Math.Sqrt(Variance);

        public void Update(double value)
        {
            Count++;
            var delta = value - Mean;
            Mean += delta / Count;
            var delta2 = value - Mean;
            _m2 += delta * delta2;
        }
    }

    private class ProfileStats
    {
        public readonly WelfordAccumulator CpuTemp = new();
        public readonly WelfordAccumulator CpuClock = new();
        public readonly WelfordAccumulator CpuPower = new();

        public readonly WelfordAccumulator GpuTemp = new();
        public readonly WelfordAccumulator GpuClock = new();
        public readonly WelfordAccumulator GpuPower = new();

        public DateTimeOffset LastUpdated = DateTimeOffset.UtcNow;
    }

    private readonly ConcurrentDictionary<WorkloadType, ProfileStats> _profiles = new();

    public void IngestSample(TelemetrySample sample, WorkloadType workload)
    {
        var stats = _profiles.GetOrAdd(workload, _ => new ProfileStats());

        lock (stats)
        {
            if (sample.CpuTemperatureC.HasValue) stats.CpuTemp.Update(sample.CpuTemperatureC.Value);
            if (sample.CpuClockMhz.HasValue) stats.CpuClock.Update(sample.CpuClockMhz.Value);
            if (sample.CpuPowerWatts.HasValue) stats.CpuPower.Update(sample.CpuPowerWatts.Value);

            if (sample.GpuTemperatureC.HasValue) stats.GpuTemp.Update(sample.GpuTemperatureC.Value);
            if (sample.GpuClockMhz.HasValue) stats.GpuClock.Update(sample.GpuClockMhz.Value);
            if (sample.GpuPowerWatts.HasValue) stats.GpuPower.Update(sample.GpuPowerWatts.Value);

            stats.LastUpdated = sample.Timestamp;
        }
    }

    public WorkloadBaseline? GetBaseline(WorkloadType workload)
    {
        if (!_profiles.TryGetValue(workload, out var stats) || stats.CpuTemp.Count < 5)
        {
            return null; // Not enough samples to establish statistical confidence
        }

        lock (stats)
        {
            return new WorkloadBaseline
            {
                ProfileName = $"Learned Profile ({workload})",
                Workload = workload,
                SampleCount = stats.CpuTemp.Count,
                AvgCpuTempC = stats.CpuTemp.Mean,
                StdDevCpuTempC = stats.CpuTemp.StdDev,
                AvgCpuClockMhz = stats.CpuClock.Mean,
                AvgCpuPowerW = stats.CpuPower.Mean,
                AvgGpuTempC = stats.GpuTemp.Mean,
                StdDevGpuTempC = stats.GpuTemp.StdDev,
                AvgGpuClockMhz = stats.GpuClock.Mean,
                AvgGpuPowerW = stats.GpuPower.Mean,
                UpdatedAt = stats.LastUpdated
            };
        }
    }

    public IReadOnlyCollection<WorkloadBaseline> GetAllBaselines()
    {
        var list = new List<WorkloadBaseline>();
        foreach (var kvp in _profiles)
        {
            var b = GetBaseline(kvp.Key);
            if (b != null) list.Add(b);
        }
        return list;
    }

    public void Reset(WorkloadType? workload = null)
    {
        if (workload.HasValue)
        {
            _profiles.TryRemove(workload.Value, out _);
        }
        else
        {
            _profiles.Clear();
        }
    }
}
