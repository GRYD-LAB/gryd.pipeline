namespace Gryd.Pipeline;

/// <summary>
/// Represents the execution record of a single pipeline step.
/// This is used for diagnostics, debugging, testing and observability.
/// </summary>
public sealed class StepExecution
{
  /// <summary>
  /// Name of the executed step.
  /// </summary>
  public required string StepName { get; init; }

  /// <summary>
  /// Timestamp when execution started.
  /// </summary>
  public DateTimeOffset StartedAt { get; init; }

  /// <summary>
  /// Timestamp when execution finished.
  /// </summary>
  public DateTimeOffset FinishedAt { get; init; }

  /// <summary>
  /// Indicates whether the step completed without throwing an exception.
  /// Success = true does NOT imply that the step performed work.
  /// A step may execute successfully and do nothing.
  /// </summary>
  public bool Success { get; init; }

  /// <summary>
  /// Indicates whether the step requested pipeline continuation.
  /// This reflects the StepResult.ShouldContinue value returned by the step.
  /// Used for observability and debugging only.
  /// </summary>
  public bool Continued { get; init; }

  /// <summary>
  /// Optional error information if the step failed.
  /// </summary>
  public Exception? Error { get; init; }
}
