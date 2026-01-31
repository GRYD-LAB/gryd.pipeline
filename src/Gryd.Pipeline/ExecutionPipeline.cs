namespace Gryd.Pipeline;

/// <summary>
/// Represents a pipeline definition composed of multiple steps.
/// </summary>
public sealed class ExecutionPipeline
{
  /// <summary>
  /// The ordered collection of steps in this pipeline.
  /// </summary>
  public IReadOnlyList<IPipelineStep> Steps { get; }

  public ExecutionPipeline(IEnumerable<IPipelineStep> steps)
  {
    Steps = steps.ToList().AsReadOnly();
  }
}
