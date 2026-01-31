namespace Gryd.Pipeline.Examples;

using Steps;

/// <summary>
/// Examples demonstrating custom step implementations.
/// </summary>
public static class CustomStepExamples
{
    /// <summary>
    /// Example of a custom validation step.
    /// </summary>
    public static async Task CustomValidationStepExample()
    {
        var pipeline = new PipelineBuilder()
            .With(new ValidationStep())
            .With(new TransformStep("Process", ctx =>
            {
                Console.WriteLine("Processing valid data...");
                return Task.CompletedTask;
            }))
            .Build();

        var runner = new PipelineRunner();
        var context = new ExecutionPipelineContext();
        context.Set("value", -5);

        await runner.RunAsync(pipeline, context);

        // Second step won't execute if validation fails
        Console.WriteLine($"Steps executed: {context.Executions.Count}");
    }

    /// <summary>
    /// Example of a custom retry step wrapper.
    /// </summary>
    public static async Task CustomRetryStepExample()
    {
        var unreliableStep = new TransformStep("UnreliableOperation", ctx =>
        {
            ctx.TryGet("attempts", out int attempts);
            ctx.Set("attempts", attempts + 1);

            if (attempts < 2)
            {
                throw new InvalidOperationException("Simulated failure");
            }

            ctx.Set("result", "Success!");
            return Task.CompletedTask;
        });

        var retryStep = new RetryStep(unreliableStep, maxRetries: 3);

        var pipeline = new PipelineBuilder()
            .With(retryStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new ExecutionPipelineContext();

        await runner.RunAsync(pipeline, context);

        var result = context.Get<string>("result");
        Console.WriteLine(result);
    }

    /// <summary>
    /// Example of a custom logging step.
    /// </summary>
    public static async Task CustomLoggingStepExample()
    {
        var pipeline = new PipelineBuilder()
            .With(new LoggingStep("Step1"))
            .With(new TransformStep("Process", ctx =>
            {
                ctx.Set("data", "processed");
                return Task.CompletedTask;
            }))
            .With(new LoggingStep("Step2"))
            .Build();

        var runner = new PipelineRunner();
        await runner.RunAsync(pipeline);
    }

    /// <summary>
    /// Example of a custom conditional branching step.
    /// </summary>
    public static async Task CustomConditionalStepExample()
    {
        var conditionalStep = new ConditionalExecutionStep(
            condition: ctx => ctx.Get<bool>("should_process"),
            stepToExecute: new TransformStep("ConditionalWork", ctx =>
            {
                Console.WriteLine("Condition was true, executing work...");
                return Task.CompletedTask;
            }));

        var pipeline = new PipelineBuilder()
            .With(conditionalStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new ExecutionPipelineContext();
        context.Set("should_process", true);

        await runner.RunAsync(pipeline, context);
    }
}

/// <summary>
/// Custom validation step that stops execution on invalid data.
/// </summary>
public class ValidationStep : IPipelineStep
{
    public string Name => "ValidateInput";

    public Task<StepResult> ExecuteAsync(
        ExecutionPipelineContext context,
        CancellationToken cancellationToken)
    {
        var value = context.Get<int>("value");

        if (value < 0)
        {
            context.Set("validation_error", "Value must be non-negative");
            Console.WriteLine("Validation failed: negative value");
            return Task.FromResult(StepResult.Stop());
        }

        context.Set("validation_passed", true);
        return Task.FromResult(StepResult.Continue());
    }
}

/// <summary>
/// Custom step that wraps another step with retry logic.
/// </summary>
public class RetryStep : IPipelineStep
{
    private readonly IPipelineStep _innerStep;
    private readonly int _maxRetries;

    public string Name => $"Retry({_innerStep.Name})";

    public RetryStep(IPipelineStep innerStep, int maxRetries = 3)
    {
        _innerStep = innerStep;
        _maxRetries = maxRetries;
    }

    public async Task<StepResult> ExecuteAsync(
        ExecutionPipelineContext context,
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        Exception? lastException = null;

        while (attempts < _maxRetries)
        {
            try
            {
                attempts++;
                var result = await _innerStep.ExecuteAsync(context, cancellationToken);

                if (result.ShouldContinue)
                {
                    Console.WriteLine($"Step succeeded on attempt {attempts}");
                    return result;
                }

                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Console.WriteLine($"Attempt {attempts} failed: {ex.Message}");

                if (attempts < _maxRetries)
                {
                    await Task.Delay(100 * attempts, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"Step failed after {_maxRetries} attempts",
            lastException);
    }
}

/// <summary>
/// Custom step that logs context state.
/// </summary>
public class LoggingStep : IPipelineStep
{
    public string Name { get; }

    public LoggingStep(string name)
    {
        Name = $"Log_{name}";
    }

    public Task<StepResult> ExecuteAsync(
        ExecutionPipelineContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{Name}] Logging context state:");

        // Log execution count
        Console.WriteLine($"  Steps executed so far: {context.Executions.Count}");

        // In a real implementation, you could inspect context.Data
        // but since it's internal, we just demonstrate the pattern

        return Task.FromResult(StepResult.Continue());
    }
}

/// <summary>
/// Custom step that conditionally executes another step.
/// </summary>
public class ConditionalExecutionStep : IPipelineStep
{
    private readonly Func<ExecutionPipelineContext, bool> _condition;
    private readonly IPipelineStep _stepToExecute;

    public string Name => $"Conditional({_stepToExecute.Name})";

    public ConditionalExecutionStep(
        Func<ExecutionPipelineContext, bool> condition,
        IPipelineStep stepToExecute)
    {
        _condition = condition;
        _stepToExecute = stepToExecute;
    }

    public async Task<StepResult> ExecuteAsync(
        ExecutionPipelineContext context,
        CancellationToken cancellationToken)
    {
        if (_condition(context))
        {
            Console.WriteLine($"Condition met, executing {_stepToExecute.Name}");
            return await _stepToExecute.ExecuteAsync(context, cancellationToken);
        }

        Console.WriteLine($"Condition not met, skipping {_stepToExecute.Name}");
        return StepResult.Continue();
    }
}
