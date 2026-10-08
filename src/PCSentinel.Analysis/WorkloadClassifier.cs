using PCSentinel.Core.Interfaces;
using PCSentinel.Core.Models;

namespace PCSentinel.Analysis;

/// <summary>
/// Deterministic real-time workload classifier that evaluates telemetry to identify
/// whether the machine is Idle, Gaming, undergoing CPU compilation/rendering, or GPU compute.
/// </summary>
public sealed class WorkloadClassifier : IWorkloadClassifier
{
    public WorkloadClassification Classify(TelemetrySample sample)
    {
        var cpuLoad = sample.CpuLoadPercent ?? 0.0;
        var gpuLoad = sample.GpuLoadPercent ?? 0.0;
        var ramLoad = sample.RamLoadPercent ?? 0.0;
        var now = sample.Timestamp;

        // 1. Idle Detection
        if (cpuLoad < 18.0 && gpuLoad < 12.0)
        {
            return new WorkloadClassification
            {
                Workload = WorkloadType.Idle,
                Confidence = 0.95,
                Description = "System Idle / Low Activity",
                Timestamp = now
            };
        }

        // 2. Gaming Workload (high GPU load with concurrent CPU activity)
        if (gpuLoad >= 70.0 && cpuLoad >= 20.0)
        {
            return new WorkloadClassification
            {
                Workload = WorkloadType.Gaming,
                Confidence = 0.92,
                Description = $"3D Gaming Load (GPU: {gpuLoad:F0}%, CPU: {cpuLoad:F0}%)",
                Timestamp = now
            };
        }

        // 3. GPU Heavy (Compute, FurMark, GPU Rendering)
        if (gpuLoad >= 80.0 && cpuLoad < 20.0)
        {
            return new WorkloadClassification
            {
                Workload = WorkloadType.GpuHeavy,
                Confidence = 0.94,
                Description = $"GPU Compute / Stress (GPU: {gpuLoad:F0}%)",
                Timestamp = now
            };
        }

        // 4. CPU Heavy (Compilation, Blender CPU, Cinebench, Video Encoding)
        if (cpuLoad >= 70.0 && gpuLoad < 30.0)
        {
            return new WorkloadClassification
            {
                Workload = WorkloadType.CpuHeavy,
                Confidence = 0.95,
                Description = $"CPU Compute / Compilation (CPU: {cpuLoad:F0}%)",
                Timestamp = now
            };
        }

        // 5. Memory Heavy
        if (ramLoad >= 88.0)
        {
            return new WorkloadClassification
            {
                Workload = WorkloadType.MemoryHeavy,
                Confidence = 0.88,
                Description = $"High Memory Pressure (RAM: {ramLoad:F0}%)",
                Timestamp = now
            };
        }

        // Mixed/Generic
        return new WorkloadClassification
        {
            Workload = WorkloadType.Custom,
            Confidence = 0.70,
            Description = $"Mixed Workload (CPU: {cpuLoad:F0}%, GPU: {gpuLoad:F0}%)",
            Timestamp = now
        };
    }
}
