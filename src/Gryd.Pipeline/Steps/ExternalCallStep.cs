namespace Gryd.Pipeline.Steps;

/// <summary>
/// Pipeline step that integrates with an external system
/// and enriches the execution context with the result.
/// </summary>
public sealed class ExternalCallStep<TOutput> : IPipelineStep
{
  /// <summary>
  /// Logical name of the step, used for observability and debugging.
  /// </summary>
  public string Name { get; }

  /// <summary>
  /// Function that calls the external system.
  /// </summary>
  public Func<PipelineExecutionContext, Task<TOutput>> Call { get; }

  /// <summary>
  /// Action that stores the result in the context.
  /// </summary>
  public Action<PipelineExecutionContext, TOutput> SaveResult { get; }

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

  public ExternalCallStep(
    string name,
    Func<PipelineExecutionContext, Task<TOutput>> call,
    Action<PipelineExecutionContext, TOutput> saveResult,
    Func<PipelineExecutionContext, bool>? executionCondition = null,
    Func<PipelineExecutionContext, bool>? continuationCondition = null)
  {
    Name = name;
    Call = call;
    SaveResult = saveResult;
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
      // Call external system and save result
      var result = await Call(context);
      SaveResult(context, result);
    }

    // Decide whether pipeline should continue (independent of execution)
    return ContinuationCondition(context)
      ? StepResult.Continue()
      : StepResult.Stop();
  }
}
