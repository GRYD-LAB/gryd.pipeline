namespace Gryd.Pipeline.Examples.Fakes;

using Steps;

/// <summary>
/// Simple concrete implementation of TransformStep for example purposes.
/// Accepts a name and action to execute.
/// </summary>
public class SimpleTransformStep : TransformStep
{
  private readonly string _name;
  private readonly Action<ExecutionPipelineContext> _action;

  public override string Name => _name;

  public SimpleTransformStep(string name, Action<ExecutionPipelineContext> action)
  {
    _name = name;
    _action = action;
  }

  protected override void Execute(ExecutionPipelineContext context)
  {
    _action(context);
  }
}
