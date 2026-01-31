namespace Gryd.Pipeline.Tests;

using Steps;

/// <summary>
/// Tests demonstrating middleware-like behavior where steps decide
/// whether to execute and whether to continue the pipeline.
/// </summary>
public class MiddlewareBehaviorTests
{
  [Fact]
  public async Task Step_Can_Skip_Execution_But_Continue_Pipeline()
  {
    // Arrange: Step that only executes if "should_execute" is true
    var conditionalStep = new TransformStep(
      "ConditionalStep",
      ctx =>
      {
        ctx.Set("executed", true);
        return Task.CompletedTask;
      },
      executionCondition: ctx => ctx.Get<bool>("should_execute"));

    var alwaysExecuteStep = new TransformStep(
      "AlwaysExecute",
      ctx =>
      {
        ctx.Set("always_ran", true);
        return Task.CompletedTask;
      });

    var pipeline = new PipelineBuilder()
      .With(conditionalStep)
      .With(alwaysExecuteStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("should_execute", false);

    // Act
    var result = await runner.RunAsync(pipeline, context, CancellationToken.None);

    // Assert: First step skipped but second still ran
    Assert.False(result.ContainsKey("executed"));
    Assert.True(result.Get<bool>("always_ran"));
    Assert.Equal(2, result.Executions.Count);
  }

  [Fact]
  public async Task Step_Can_Execute_And_Stop_Pipeline()
  {
    // Arrange: Step that stops pipeline based on validation
    var validationStep = new TransformStep(
      "Validation",
      ctx =>
      {
        var value = ctx.Get<int>("value");
        ctx.Set("is_valid", value > 0);
        return Task.CompletedTask;
      },
      continuationCondition: ctx => ctx.Get<bool>("is_valid"));

    var processingStep = new TransformStep(
      "Processing",
      ctx =>
      {
        ctx.Set("processed", true);
        return Task.CompletedTask;
      });

    var pipeline = new PipelineBuilder()
      .With(validationStep)
      .With(processingStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("value", -5);

    // Act
    var result = await runner.RunAsync(pipeline, context, CancellationToken.None);

    // Assert: Validation ran and stopped, processing never ran
    Assert.False(result.Get<bool>("is_valid"));
    Assert.False(result.ContainsKey("processed"));
    Assert.Single(result.Executions);
    Assert.Equal("Validation", result.Executions[0].StepName);
  }

  [Fact]
  public async Task Step_Can_Skip_And_Stop_Pipeline()
  {
    // Arrange: Guard step that requires initialization
    var guardStep = new TransformStep(
      "Guard",
      ctx =>
      {
        ctx.Set("guard_passed", true);
        return Task.CompletedTask;
      },
      executionCondition: ctx => ctx.Get<bool>("initialized"),
      continuationCondition: ctx => ctx.ContainsKey("guard_passed"));

    var workStep = new TransformStep(
      "Work",
      ctx =>
      {
        ctx.Set("work_done", true);
        return Task.CompletedTask;
      });

    var pipeline = new PipelineBuilder()
      .With(guardStep)
      .With(workStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("initialized", false);

    // Act
    var result = await runner.RunAsync(pipeline, context, CancellationToken.None);

    // Assert: Guard skipped execution and stopped pipeline
    Assert.False(result.ContainsKey("guard_passed"));
    Assert.False(result.ContainsKey("work_done"));
    Assert.Single(result.Executions);
  }

  [Fact]
  public async Task Multiple_Steps_Can_Independently_Control_Flow()
  {
    // Arrange: Complex pipeline with various flow control patterns
    var step1 = new TransformStep(
      "AlwaysExecute",
      ctx =>
      {
        ctx.Set("step1", true);
        return Task.CompletedTask;
      });

    var step2 = new TransformStep(
      "ConditionalExecute",
      ctx =>
      {
        ctx.Set("step2", true);
        return Task.CompletedTask;
      },
      executionCondition: ctx => ctx.Get<bool>("run_step2"));

    var step3 = new TransformStep(
      "MaybeStop",
      ctx =>
      {
        ctx.Set("step3", true);
        return Task.CompletedTask;
      },
      continuationCondition: ctx => ctx.Get<bool>("continue_after_step3"));

    var step4 = new TransformStep(
      "NeverReached",
      ctx =>
      {
        ctx.Set("step4", true);
        return Task.CompletedTask;
      });

    var pipeline = new PipelineBuilder()
      .With(step1)
      .With(step2)
      .With(step3)
      .With(step4)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("run_step2", false);
    context.Set("continue_after_step3", false);

    // Act
    var result = await runner.RunAsync(pipeline, context, CancellationToken.None);

    // Assert
    Assert.True(result.Get<bool>("step1")); // Executed
    Assert.False(result.ContainsKey("step2")); // Skipped
    Assert.True(result.Get<bool>("step3")); // Executed
    Assert.False(result.ContainsKey("step4")); // Never reached (step3 stopped)
    Assert.Equal(3, result.Executions.Count);
  }

  [Fact]
  public async Task Authentication_Pattern_With_Early_Exit()
  {
    // Arrange: Real-world authentication pattern
    var authStep = new TransformStep(
      "Authenticate",
      ctx =>
      {
        var token = ctx.Get<string>("auth_token");
        var isValid = token == "valid_token";
        ctx.Set("authenticated", isValid);
        ctx.Set("user_id", isValid ? "user123" : null);
        return Task.CompletedTask;
      },
      continuationCondition: ctx => ctx.Get<bool>("authenticated"));

    var fetchDataStep = new TransformStep(
      "FetchData",
      ctx =>
      {
        var userId = ctx.Get<string>("user_id");
        ctx.Set("data", $"Data for {userId}");
        return Task.CompletedTask;
      });

    var pipeline = new PipelineBuilder()
      .With(authStep)
      .With(fetchDataStep)
      .Build();

    var runner = new PipelineRunner();

    // Act: Invalid token
    var context1 = new ExecutionPipelineContext();
    context1.Set("auth_token", "invalid");
    var result1 = await runner.RunAsync(pipeline, context1, CancellationToken.None);

    // Act: Valid token
    var context2 = new ExecutionPipelineContext();
    context2.Set("auth_token", "valid_token");
    var result2 = await runner.RunAsync(pipeline, context2, CancellationToken.None);

    // Assert: Invalid token stops pipeline
    Assert.False(result1.Get<bool>("authenticated"));
    Assert.False(result1.ContainsKey("data"));
    Assert.Single(result1.Executions);

    // Assert: Valid token continues
    Assert.True(result2.Get<bool>("authenticated"));
    Assert.Equal("Data for user123", result2.Get<string>("data"));
    Assert.Equal(2, result2.Executions.Count);
  }

  [Fact]
  public async Task Rate_Limiting_Pattern_With_Conditional_Execution()
  {
    // Arrange: Rate limiting that only applies to non-premium users
    var rateLimitStep = new TransformStep(
      "CheckRateLimit",
      ctx =>
      {
        var requestCount = ctx.Get<int>("request_count");
        var withinLimit = requestCount < 100;
        ctx.Set("rate_limit_ok", withinLimit);
        return Task.CompletedTask;
      },
      executionCondition: ctx => !ctx.Get<bool>("is_premium"),
      continuationCondition: ctx =>
      {
        // If didn't execute (premium user), always continue
        if (!ctx.ContainsKey("rate_limit_ok"))
          return true;
        // If executed, check the limit
        return ctx.Get<bool>("rate_limit_ok");
      });

    var processStep = new TransformStep(
      "Process",
      ctx =>
      {
        ctx.Set("processed", true);
        return Task.CompletedTask;
      });

    var pipeline = new PipelineBuilder()
      .With(rateLimitStep)
      .With(processStep)
      .Build();

    var runner = new PipelineRunner();

    // Act: Premium user with high request count (should skip rate limit)
    var premiumContext = new ExecutionPipelineContext();
    premiumContext.Set("is_premium", true);
    premiumContext.Set("request_count", 1000);
    var premiumResult = await runner.RunAsync(pipeline, premiumContext, CancellationToken.None);

    // Act: Regular user over limit (should stop)
    var overLimitContext = new ExecutionPipelineContext();
    overLimitContext.Set("is_premium", false);
    overLimitContext.Set("request_count", 150);
    var overLimitResult = await runner.RunAsync(pipeline, overLimitContext, CancellationToken.None);

    // Act: Regular user within limit (should continue)
    var withinLimitContext = new ExecutionPipelineContext();
    withinLimitContext.Set("is_premium", false);
    withinLimitContext.Set("request_count", 50);
    var withinLimitResult = await runner.RunAsync(pipeline, withinLimitContext, CancellationToken.None);

    // Assert: Premium user bypasses rate limit
    Assert.False(premiumResult.ContainsKey("rate_limit_ok"));
    Assert.True(premiumResult.Get<bool>("processed"));
    Assert.Equal(2, premiumResult.Executions.Count);

    // Assert: Over limit user stopped
    Assert.False(overLimitResult.Get<bool>("rate_limit_ok"));
    Assert.False(overLimitResult.ContainsKey("processed"));
    Assert.Single(overLimitResult.Executions);

    // Assert: Within limit user continues
    Assert.True(withinLimitResult.Get<bool>("rate_limit_ok"));
    Assert.True(withinLimitResult.Get<bool>("processed"));
    Assert.Equal(2, withinLimitResult.Executions.Count);
  }
}
