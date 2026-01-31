namespace Gryd.Pipeline.Tests;

using Steps;

/// <summary>
/// Tests demonstrating the new Continued property for flow control observability.
/// </summary>
public class FlowControlObservabilityTests
{
    [Fact]
    public async Task StepExecution_Should_Record_Continued_True_When_Step_Continues()
    {
        // Arrange
        var step = new TransformStep(
            "ContinuingStep",
            ctx =>
            {
                ctx.Set("executed", true);
                return Task.CompletedTask;
            });

        var pipeline = new PipelineBuilder()
            .With(step)
            .Build();

        var runner = new PipelineRunner();

        // Act
        var context = await runner.RunAsync(pipeline);

        // Assert
        Assert.Single(context.Executions);
        var execution = context.Executions[0];
        Assert.True(execution.Success);
        Assert.True(execution.Continued);
        Assert.Null(execution.Error);
    }

    [Fact]
    public async Task StepExecution_Should_Record_Continued_False_When_Step_Stops()
    {
        // Arrange
        var stopStep = new TransformStep(
            "StoppingStep",
            ctx =>
            {
                ctx.Set("executed", true);
                return Task.CompletedTask;
            },
            continuationCondition: _ => false);  // Always stop

        var neverReachedStep = new TransformStep(
            "NeverReached",
            ctx =>
            {
                ctx.Set("should_not_execute", true);
                return Task.CompletedTask;
            });

        var pipeline = new PipelineBuilder()
            .With(stopStep)
            .With(neverReachedStep)
            .Build();

        var runner = new PipelineRunner();

        // Act
        var context = await runner.RunAsync(pipeline);

        // Assert: Only first step executed
        Assert.Single(context.Executions);
        var execution = context.Executions[0];

        Assert.Equal("StoppingStep", execution.StepName);
        Assert.True(execution.Success);
        Assert.False(execution.Continued);  // Step requested stop
        Assert.Null(execution.Error);

        // Verify second step never ran
        Assert.False(context.ContainsKey("should_not_execute"));
    }

    [Fact]
    public async Task StepExecution_Should_Record_Continued_False_On_Exception()
    {
        // Arrange
        var failingStep = new TransformStep(
            "FailingStep",
            ctx =>
            {
                throw new InvalidOperationException("Test failure");
            });

        var pipeline = new PipelineBuilder()
            .With(failingStep)
            .Build();

        var runner = new PipelineRunner();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(pipeline));

        // Verify execution was recorded
        var context = new ExecutionPipelineContext();
        try
        {
            await runner.RunAsync(pipeline, context);
        }
        catch
        {
            // Expected
        }

        Assert.Single(context.Executions);
        var execution = context.Executions[0];

        Assert.Equal("FailingStep", execution.StepName);
        Assert.False(execution.Success);
        Assert.False(execution.Continued);  // Exception prevents continuation
        Assert.NotNull(execution.Error);
        Assert.Equal("Test failure", execution.Error.Message);
    }

    [Fact]
    public async Task Can_Identify_Which_Step_Stopped_Pipeline()
    {
        // Arrange: Multi-step pipeline where second step stops
        var step1 = new TransformStep("Step1", ctx => Task.CompletedTask);
        var step2 = new TransformStep(
            "Step2",
            ctx => Task.CompletedTask,
            continuationCondition: _ => false);  // Stop here
        var step3 = new TransformStep("Step3", ctx => Task.CompletedTask);

        var pipeline = new PipelineBuilder()
            .With(step1)
            .With(step2)
            .With(step3)
            .Build();

        var runner = new PipelineRunner();

        // Act
        var context = await runner.RunAsync(pipeline);

        // Assert: Can observe flow control decisions
        Assert.Equal(2, context.Executions.Count);

        Assert.True(context.Executions[0].Continued);   // Step1 continued
        Assert.False(context.Executions[1].Continued);  // Step2 stopped

        // Can identify the stopping step
        var stoppingStep = context.Executions.FirstOrDefault(e => !e.Continued);
        Assert.NotNull(stoppingStep);
        Assert.Equal("Step2", stoppingStep.StepName);
    }

    [Fact]
    public async Task Success_True_Does_Not_Imply_Work_Was_Done()
    {
        // Arrange: Step that does nothing based on condition
        var conditionalStep = new TransformStep(
            "ConditionalWork",
            ctx =>
            {
                if (ctx.Get<bool>("do_work"))
                {
                    ctx.Set("work_done", true);
                }
                // If do_work is false, step does nothing but still succeeds
                return Task.CompletedTask;
            });

        var pipeline = new PipelineBuilder()
            .With(conditionalStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new ExecutionPipelineContext();
        context.Set("do_work", false);  // Don't do work

        // Act
        var result = await runner.RunAsync(pipeline, context);

        // Assert: Step succeeded even though it did nothing
        Assert.Single(result.Executions);
        var execution = result.Executions[0];

        Assert.True(execution.Success);  // No exception = success
        Assert.True(execution.Continued);
        Assert.False(result.ContainsKey("work_done"));  // But no work was done

        // Success does NOT mean work was performed
        // It only means no exception was thrown
    }

    [Fact]
    public async Task Observability_Pattern_Trace_Pipeline_Execution()
    {
        // Arrange: Realistic pipeline with various flow control
        var authenticate = new TransformStep(
            "Authenticate",
            ctx => { ctx.Set("user_id", "user123"); return Task.CompletedTask; });

        var checkPermissions = new TransformStep(
            "CheckPermissions",
            ctx => { ctx.Set("has_permission", true); return Task.CompletedTask; },
            continuationCondition: ctx => ctx.Get<bool>("has_permission"));

        var fetchData = new TransformStep(
            "FetchData",
            ctx => { ctx.Set("data", "sensitive_data"); return Task.CompletedTask; });

        var pipeline = new PipelineBuilder()
            .With(authenticate)
            .With(checkPermissions)
            .With(fetchData)
            .Build();

        var runner = new PipelineRunner();

        // Act
        var context = await runner.RunAsync(pipeline);

        // Assert: Can trace entire execution flow
        Assert.Equal(3, context.Executions.Count);

        // All steps succeeded
        Assert.All(context.Executions, e => Assert.True(e.Success));

        // All steps continued
        Assert.All(context.Executions, e => Assert.True(e.Continued));

        // Can measure pipeline duration
        var totalDuration = context.Executions.Sum(e =>
            (e.FinishedAt - e.StartedAt).TotalMilliseconds);
        Assert.True(totalDuration >= 0);

        // Can see execution order
        Assert.Equal("Authenticate", context.Executions[0].StepName);
        Assert.Equal("CheckPermissions", context.Executions[1].StepName);
        Assert.Equal("FetchData", context.Executions[2].StepName);
    }
}
