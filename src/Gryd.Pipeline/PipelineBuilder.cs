namespace Gryd.Pipeline;

/// <summary>
/// Fluent builder used to compose a pipeline definition.
/// No execution happens at this stage.
/// </summary>
public sealed class PipelineBuilder
{
  private readonly IList<IPipelineStep> _steps = new List<IPipelineStep>();

  /// <summary>
  /// Adds a step to the pipeline.
  /// </summary>
  public PipelineBuilder With(IPipelineStep step)
  {
    _steps.Add(step);
    return this;
  }

  /// <summary>
  /// Builds the pipeline from the configured steps.
  /// </summary>
  public Pipeline Build()
  {
    return new Pipeline(_steps);
  }
}
