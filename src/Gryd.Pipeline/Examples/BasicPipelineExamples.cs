namespace Gryd.Pipeline.Examples;

using Steps;

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
    var step1 = new TransformStep(
      "EnrichContext",
      ctx =>
      {
        ctx.Set("user_input", "Hello, world!");
        return Task.CompletedTask;
      });

    var step2 = new TransformStep(
      "ProcessData",
      ctx =>
      {
        var input = ctx.Get<string>("user_input");
        var result = input.ToUpper();
        ctx.Set("processed", result);
        return Task.CompletedTask;
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
    var validateStep = new TransformStep(
      "ValidateInput",
      ctx =>
      {
        var value = ctx.Get<int>("input_value");

        if (value < 0)
        {
          ctx.Set("error", "Negative values not allowed");
          return Task.FromResult(StepResult.Stop());
        }

        return Task.FromResult(StepResult.Continue());
      });

    var processStep = new TransformStep(
      "ProcessValue",
      ctx =>
      {
        var value = ctx.Get<int>("input_value");
        ctx.Set("result", value * 2);
        return Task.CompletedTask;
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
    var step1 = new TransformStep("Step1", ctx =>
    {
      ctx.Set("data", "test");
      return Task.CompletedTask;
    });

    var step2 = new TransformStep("Step2", ctx =>
    {
      var data = ctx.Get<string>("data");
      ctx.Set("processed", data.ToUpper());
      return Task.CompletedTask;
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
    var validateStep = new TransformStep("Validate", ctx =>
    {
      var input = ctx.Get<string>("input");
      ctx.Set("valid", !string.IsNullOrEmpty(input));
      return Task.CompletedTask;
    });

    var processStep = new TransformStep("Process", ctx =>
    {
      var input = ctx.Get<string>("input");
      ctx.Set("output", input.Trim().ToLower());
      return Task.CompletedTask;
    });

    var enrichStep = new TransformStep("Enrich", ctx =>
    {
      var output = ctx.Get<string>("output");
      ctx.Set("final", $"Processed: {output}");
      return Task.CompletedTask;
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
