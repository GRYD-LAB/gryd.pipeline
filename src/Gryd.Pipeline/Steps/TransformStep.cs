namespace Gryd.Pipeline.Steps;

/// <summary>
/// Pipeline step that performs in-memory transformations
/// over data stored in the execution context.
/// </summary>
public sealed class TransformStep : IPipelineStep
{
  /// <summary>
  /// Logical name of the step, used for observability and debugging.
  /// </summary>
  public string Name { get; }

  /// <summary>
  /// Handler that performs transformations and enriches the context.
  /// </summary>
  public Func<PipelineExecutionContext, Task> Handler { get; }

  /// <summary>
  /// Predicate to determine if this step should execute.
  /// If false, the step returns StepResult.Continue() without doing work.
  /// </summary>
  public Func<PipelineExecutionContext, bool> ExecutionCondition { get; }

  /// <summary>
  /// Function to determine the flow control decision after execution.
  /// Receives the context and returns whether to continue (true) or stop (false).
  /// </summary>
  public Func<PipelineExecutionContext, bool> ContinuationCondition { get; }

  public TransformStep(
    string name,
    Func<PipelineExecutionContext, Task> handler,
    Func<PipelineExecutionContext, bool>? executionCondition = null,
    Func<PipelineExecutionContext, bool>? continuationCondition = null)
  {
    Name = name;
    Handler = handler;
    ExecutionCondition = executionCondition ?? (_ => true);
    ContinuationCondition = continuationCondition ?? (_ => true);
  }

  public async Task<StepResult> ExecuteAsync(
    PipelineExecutionContext context,
    CancellationToken ct
  )
  {
    // The step decides whether to perform work
    if (ExecutionCondition(context))
    {
      // Perform the transformation
      await Handler(context);
    }

    // Decide whether pipeline should continue (independent of execution)
    return ContinuationCondition(context)
      ? StepResult.Continue()
      : StepResult.Stop();
  }
}
