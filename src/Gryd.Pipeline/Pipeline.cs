namespace Gryd.Pipeline;

/// <summary>
/// Represents a pipeline definition composed of multiple steps.
/// </summary>
public sealed class Pipeline
{
  /// <summary>
  /// The ordered collection of steps in this pipeline.
  /// </summary>
  public IReadOnlyList<IPipelineStep> Steps { get; }

  public Pipeline(IEnumerable<IPipelineStep> steps)
  {
    Steps = steps.ToList().AsReadOnly();
  }
}
