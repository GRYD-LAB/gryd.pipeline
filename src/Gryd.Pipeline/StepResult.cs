namespace Gryd.Pipeline;

/// <summary>
/// Result of a step execution, controlling pipeline flow.
/// </summary>
public sealed class StepResult
{
  /// <summary>
  /// Indicates whether pipeline execution should continue.
  /// </summary>
  public bool ShouldContinue { get; }

  private StepResult(bool shouldContinue)
  {
    ShouldContinue = shouldContinue;
  }

  /// <summary>
  /// Continues execution to the next step.
  /// </summary>
  public static StepResult Continue() => new(true);

  /// <summary>
  /// Stops pipeline execution immediately.
  /// </summary>
  public static StepResult Stop() => new(false);
}
