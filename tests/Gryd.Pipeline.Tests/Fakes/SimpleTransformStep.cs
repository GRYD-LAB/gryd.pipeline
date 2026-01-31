namespace Gryd.Pipeline.Tests.Fakes;

using Steps;

/// <summary>
/// Simple concrete implementation of TransformStep for testing purposes.
/// Accepts a name and action to execute.
/// </summary>
public class SimpleTransformStep : IPipelineStep
{
  private readonly string _name;
  private readonly Action<ExecutionPipelineContext> _action;
  private readonly Func<ExecutionPipelineContext, bool>? _executionCondition;
  private readonly Func<ExecutionPipelineContext, bool>? _continuationCondition;

  public string Name => _name;

  public SimpleTransformStep(
    string name,
    Action<ExecutionPipelineContext> action,
    Func<ExecutionPipelineContext, bool>? executionCondition = null,
    Func<ExecutionPipelineContext, bool>? continuationCondition = null)
  {
    _name = name;
    _action = action;
    _executionCondition = executionCondition;
    _continuationCondition = continuationCondition;
  }

  public Task<StepResult> ExecuteAsync(
    ExecutionPipelineContext context,
    CancellationToken ct)
  {
    // Check execution condition
    if (_executionCondition != null && !_executionCondition(context))
    {
      // Skip execution but respect continuation condition
      return Task.FromResult(
        _continuationCondition?.Invoke(context) != false
          ? StepResult.Continue()
          : StepResult.Stop());
    }

    // Execute the action
    _action(context);

    // Check continuation condition
    var shouldContinue = _continuationCondition?.Invoke(context) ?? true;
    return Task.FromResult(shouldContinue ? StepResult.Continue() : StepResult.Stop());
  }
}
