namespace Gryd.Pipeline.Tests;

using Steps;
using Fakes;

public class ExecutionPipelineContextTests
{
  [Fact]
  public void Set_And_Get_Should_Store_And_Retrieve_Values()
  {
    // Arrange
    var context = new ExecutionPipelineContext();

    // Act
    context.Set("key1", "value1");
    context.Set("key2", 42);

    // Assert
    Assert.Equal("value1", context.Get<string>("key1"));
    Assert.Equal(42, context.Get<int>("key2"));
  }

  [Fact]
  public void Has_Should_Return_True_For_Existing_Keys()
  {
    // Arrange
    var context = new ExecutionPipelineContext();
    context.Set("key1", "value");

    // Assert
    Assert.True(context.Has("key1"));
    Assert.False(context.Has("key2"));
  }

  [Fact]
  public void Get_Should_Throw_For_Missing_Key()
  {
    // Arrange
    var context = new ExecutionPipelineContext();

    // Act & Assert
    Assert.Throws<KeyNotFoundException>(() => context.Get<string>("missing"));
  }
}

public class TransformStepTests
{
  [Fact]
  public async Task TransformStep_Should_Execute_Handler_And_Enrich_Context()
  {
    // Arrange
    var step = new SimpleTransformStep(
      "TestTransform",
      ctx => { ctx.Set("result", "transformed"); });

    var context = new ExecutionPipelineContext();

    // Act
    var result = await step.ExecuteAsync(context, CancellationToken.None);

    // Assert
    Assert.True(result.ShouldContinue);
    Assert.Equal("transformed", context.Get<string>("result"));
  }
}

public class ExternalCallStepTests
{
  [Fact]
  public async Task ExternalCallStep_Should_Call_And_Save_Result()
  {
    // Arrange
    var step = new ExternalCallStep<int>(
      "TestExternalCall",
      ctx => Task.FromResult(42),
      (ctx, result) => ctx.Set("external_result", result));

    var context = new ExecutionPipelineContext();

    // Act
    var result = await step.ExecuteAsync(context, CancellationToken.None);

    // Assert
    Assert.True(result.ShouldContinue);
    Assert.Equal(42, context.Get<int>("external_result"));
  }
}

public class PipelineBuilderTests
{
  [Fact]
  public void PipelineBuilder_Should_Build_Pipeline_With_Steps()
  {
    // Arrange
    var step1 = new SimpleTransformStep("Step1", ctx => { });
    var step2 = new SimpleTransformStep("Step2", ctx => { });

    // Act
    var pipeline = new PipelineBuilder()
      .With(step1)
      .With(step2)
      .Build();

    // Assert
    Assert.Equal(2, pipeline.Steps.Count);
    Assert.Equal("Step1", pipeline.Steps[0].Name);
    Assert.Equal("Step2", pipeline.Steps[1].Name);
  }
}

public class PipelineRunnerTests
{
  [Fact]
  public async Task Runner_Should_Execute_All_Steps_Sequentially()
  {
    // Arrange
    var executionOrder = new List<string>();

    var step1 = new SimpleTransformStep("Step1", ctx =>
    {
      executionOrder.Add("Step1");
      ctx.Set("step1", "done");
    });

    var step2 = new SimpleTransformStep("Step2", ctx =>
    {
      executionOrder.Add("Step2");
      ctx.Set("step2", "done");
    });

    var pipeline = new PipelineBuilder()
      .With(step1)
      .With(step2)
      .Build();

    var runner = new PipelineRunner();

    // Act
    var context = await runner.RunAsync(pipeline, CancellationToken.None);

    // Assert
    Assert.Equal(2, executionOrder.Count);
    Assert.Equal("Step1", executionOrder[0]);
    Assert.Equal("Step2", executionOrder[1]);
    Assert.Equal("done", context.Get<string>("step1"));
    Assert.Equal("done", context.Get<string>("step2"));
    Assert.Equal(2, context.Executions.Count);
    Assert.True(context.Executions.All(e => e.Success));
  }

  [Fact]
  public async Task Runner_Should_Stop_When_Step_Returns_Stop()
  {
    // Arrange
    var step1 = new StopStep("StopStep");
    var step2 = new SimpleTransformStep("Step2", ctx => { ctx.Set("should_not_execute", true); });

    var pipeline = new PipelineBuilder()
      .With(step1)
      .With(step2)
      .Build();

    var runner = new PipelineRunner();

    // Act
    var context = await runner.RunAsync(pipeline, CancellationToken.None);

    // Assert
    Assert.Single(context.Executions);
    Assert.Equal("StopStep", context.Executions[0].StepName);
    Assert.False(context.Has("should_not_execute"));
  }

  [Fact]
  public async Task Runner_Should_Record_Failed_Execution_And_Rethrow()
  {
    // Arrange
    var step1 = new SimpleTransformStep("FailingStep", ctx => { throw new InvalidOperationException("Test error"); });

    var pipeline = new PipelineBuilder()
      .With(step1)
      .Build();

    var runner = new PipelineRunner();

    // Act & Assert
    var ex =
      await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(pipeline, CancellationToken.None));

    Assert.Equal("Test error", ex.Message);
  }

  // Helper step that stops the pipeline
  private class StopStep : IPipelineStep
  {
    public string Name { get; }

    public StopStep(string name)
    {
      Name = name;
    }

    public Task<StepResult> ExecuteAsync(ExecutionPipelineContext context, CancellationToken ct)
    {
      return Task.FromResult(StepResult.Stop());
    }
  }
}
