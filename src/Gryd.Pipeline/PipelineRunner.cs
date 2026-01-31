namespace Gryd.Pipeline;

/// <summary>
/// Executes a pipeline sequentially, respecting flow control signals from steps
/// and recording execution metadata.
///
/// INVARIANTS:
/// - Steps are executed exactly once, in order
/// - The runner never skips, retries, or reorders steps
/// - All flow control decisions come exclusively from StepResult
/// - The runner contains no business logic
///
/// The runner is intentionally dumb:
/// - Executes steps in order
/// - Records timing and success/failure
/// - Respects StepResult.Stop()
/// - Contains no business logic
/// </summary>
public sealed class PipelineRunner
{
  /// <summary>
  /// Runs a pipeline and returns the execution context.
  /// </summary>
  public async Task<ExecutionPipelineContext> RunAsync(
    ExecutionPipeline executionPipeline,
    CancellationToken ct = default
  )
  {
    return await RunAsync(executionPipeline, new ExecutionPipelineContext(), ct);
  }

  /// <summary>
  /// Runs a pipeline with an existing context and returns it.
  /// </summary>
  public async Task<ExecutionPipelineContext> RunAsync(
    ExecutionPipeline executionPipeline,
    ExecutionPipelineContext context,
    CancellationToken ct = default
  )
  {
    foreach (var step in executionPipeline.Steps)
    {
      var started = DateTimeOffset.UtcNow;

      try
      {
        // Execute the step - it decides whether to do work and whether to continue
        var result = await step.ExecuteAsync(context, ct);

        context.Executions.Add(new StepExecution
        {
          StepName = step.Name,
          StartedAt = started,
          FinishedAt = DateTimeOffset.UtcNow,
          Success = true,
          Continued = result.ShouldContinue
        });

        // Respect the step's flow control decision
        if (!result.ShouldContinue)
          break;
      }
      catch (Exception ex)
      {
        context.Executions.Add(new StepExecution
        {
          StepName = step.Name,
          StartedAt = started,
          FinishedAt = DateTimeOffset.UtcNow,
          Success = false,
          Continued = false,
          Error = ex
        });

        throw;
      }
    }

    return context;
  }
}
