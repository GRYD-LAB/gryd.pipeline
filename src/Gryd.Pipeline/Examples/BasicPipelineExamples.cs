namespace Gryd.Pipeline.Examples;

using Fakes;

/// <summary>
/// Basic pipeline examples demonstrating core concepts.
/// </summary>
public static class BasicPipelineExamples
{
  /// <summary>
  /// Simple two-step pipeline that transforms data.
  /// </summary>
  public static async Task SimpleTransformPipeline()
  {
    // 1. Create steps
    var step1 = new SimpleTransformStep(
      "EnrichContext",
      ctx => { ctx.Set("user_input", "Hello, world!"); });

    var step2 = new SimpleTransformStep(
      "ProcessData",
      ctx =>
      {
        var input = ctx.Get<string>("user_input");
        var result = input.ToUpper();
        ctx.Set("processed", result);
      });

    // 2. Build the pipeline
    var pipeline = new PipelineBuilder()
      .With(step1)
      .With(step2)
      .Build();

    // 3. Execute the pipeline
    var runner = new PipelineRunner();
    var context = await runner.RunAsync(pipeline, CancellationToken.None);

    // 4. Access results
    var result = context.Get<string>("processed");
    Console.WriteLine(result); // Output: HELLO, WORLD!
  }

  /// <summary>
  /// Pipeline with conditional stop execution.
  /// </summary>
  public static async Task ConditionalStopExample()
  {
    var validateStep = new ValidateInputStep();

    var processStep = new SimpleTransformStep(
      "ProcessValue",
      ctx =>
      {
        var value = ctx.Get<int>("input_value");
        ctx.Set("result", value * 2);
      });

    var pipeline = new PipelineBuilder()
      .With(validateStep)
      .With(processStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("input_value", -5);

    await runner.RunAsync(pipeline, context, CancellationToken.None);

    // The second step won't execute because validation stopped the pipeline
    Console.WriteLine($"Executions: {context.Executions.Count}"); // Output: 1
  }

  /// <summary>
  /// Demonstrates observability and debugging features.
  /// </summary>
  public static async Task ObservabilityExample()
  {
    var step1 = new SimpleTransformStep("Step1", ctx => { ctx.Set("data", "test"); });

    var step2 = new SimpleTransformStep("Step2", ctx =>
    {
      var data = ctx.Get<string>("data");
      ctx.Set("processed", data.ToUpper());
    });

    var pipeline = new PipelineBuilder()
      .With(step1)
      .With(step2)
      .Build();

    var runner = new PipelineRunner();
    var context = await runner.RunAsync(pipeline, CancellationToken.None);

    // Inspect execution history
    foreach (var execution in context.Executions)
    {
      Console.WriteLine($"Step: {execution.StepName}");
      Console.WriteLine($"Duration: {execution.FinishedAt - execution.StartedAt}");
      Console.WriteLine($"Success: {execution.Success}");

      if (execution.Error != null)
      {
        Console.WriteLine($"Error: {execution.Error.Message}");
      }
    }
  }

  /// <summary>
  /// Pipeline composition - combining multiple steps.
  /// </summary>
  public static async Task CompositionExample()
  {
    // Create individual steps
    var validateStep = new SimpleTransformStep("Validate", ctx =>
    {
      var input = ctx.Get<string>("input");
      ctx.Set("valid", !string.IsNullOrEmpty(input));
    });

    var processStep = new SimpleTransformStep("Process", ctx =>
    {
      var input = ctx.Get<string>("input");
      ctx.Set("output", input.Trim().ToLower());
    });

    var enrichStep = new SimpleTransformStep("Enrich", ctx =>
    {
      var output = ctx.Get<string>("output");
      ctx.Set("final", $"Processed: {output}");
    });

    // Combine them into a pipeline
    var pipeline = new PipelineBuilder()
      .With(validateStep)
      .With(processStep)
      .With(enrichStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("input", "  HELLO  ");

    await runner.RunAsync(pipeline, context, CancellationToken.None);

    Console.WriteLine(context.Get<string>("final")); // Output: Processed: hello
  }
}

/// <summary>
/// Custom validation step that stops execution on invalid data.
/// </summary>
public class ValidateInputStep : IPipelineStep
{
  public string Name => "ValidateInput";

  public Task<StepResult> ExecuteAsync(
    ExecutionPipelineContext context,
    CancellationToken cancellationToken)
  {
    var value = context.Get<int>("input_value");

    if (value < 0)
    {
      context.Set("error", "Negative values not allowed");
      return Task.FromResult(StepResult.Stop());
    }

    return Task.FromResult(StepResult.Continue());
  }
}
