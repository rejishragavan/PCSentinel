using PCSentinel.Core.Models;

namespace PCSentinel.Core.Interfaces;

/// <summary>
/// Evaluates system load indicators to classify what type of task the PC is running.
/// </summary>
public interface IWorkloadClassifier
{
    WorkloadClassification Classify(TelemetrySample sample);
}
