namespace Gryd.Pipeline.Steps;

/// <summary>
/// Abstract base class for pipeline steps that perform in-memory transformations
/// over data stored in the execution context.
/// </summary>
public abstract class TransformStep : IPipelineStep
{
  /// <summary>
  /// Logical name of the step, used for observability and debugging.
  /// </summary>
  public abstract string Name { get; }

  /// <summary>
  /// Executes the step asynchronously.
  /// </summary>
  public Task<StepResult> ExecuteAsync(
    ExecutionPipelineContext context,
    CancellationToken ct)
  {
    Execute(context);
    return Task.FromResult(StepResult.Continue());
  }

  /// <summary>
  /// Performs the transformation on the execution ctx.
  /// Override this method to implement your transformation logic.
  /// </summary>
  /// <param name="ctx">The execution ctx containing shared data.</param>
  protected abstract void Execute(ExecutionPipelineContext ctx);
}
