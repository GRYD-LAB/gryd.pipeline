namespace Gryd.Pipeline;

/// <summary>
/// Represents a single executable unit in the pipeline.
/// A step may call an LLM, perform data transformation,
/// call external systems, or control execution flow.
/// </summary>
public interface IPipelineStep
{
  /// <summary>
  /// Logical name of the step, used for observability and debugging.
  /// </summary>
  string Name { get; }

  /// <summary>
  /// Executes the step using the shared pipeline execution context.
  /// Returns a StepResult that determines whether the pipeline should continue.
  ///
  /// The step itself decides:
  /// - Whether to perform work or skip (internal decision)
  /// - Whether the pipeline should continue or stop (via StepResult)
  /// </summary>
  Task<StepResult> ExecuteAsync(
    PipelineExecutionContext context,
    CancellationToken ct
  );
}
