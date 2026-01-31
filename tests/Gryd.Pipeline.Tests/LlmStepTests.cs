namespace Gryd.Pipeline.Tests;

using Steps;
using Fakes;

public class LlmStepTests
{
  [Fact]
  public async Task LlmStep_Should_Render_Prompt_And_Store_Result()
  {
    // Arrange
    var provider = new FakeLlmProvider("Generated response");

    var llmStep = new LlmStep<string>(
      name: "TestLlm",
      provider: provider,
      inputMapper: ctx => new Dictionary<string, string>
      {
        ["user_name"] = ctx.Get<string>("name"),
        ["question"] = ctx.Get<string>("question")
      },
      promptTemplate: "User {user_name} asks: {question}",
      outputParser: raw => raw.ToUpper(),
      outputKey: "answer");

    var context = new ExecutionPipelineContext();
    context.Set("name", "Alice");
    context.Set("question", "What is AI?");

    // Act
    var result = await llmStep.ExecuteAsync(context, CancellationToken.None);

    // Assert
    Assert.True(result.ShouldContinue);
    Assert.Equal("GENERATED RESPONSE", context.Get<string>("answer"));
  }

  [Fact]
  public async Task LlmStep_Should_Use_Response_Generator()
  {
    // Arrange
    var provider = new FakeLlmProvider(prompt =>
      prompt.Contains("math") ? "42" : "Unknown");

    var llmStep = new LlmStep<int>(
      name: "MathLlm",
      provider: provider,
      inputMapper: ctx => new Dictionary<string, string>
      {
        ["query"] = ctx.Get<string>("query")
      },
      promptTemplate: "{query}",
      outputParser: raw => int.Parse(raw),
      outputKey: "result");

    var context = new ExecutionPipelineContext();
    context.Set("query", "Solve this math problem");

    // Act
    var result = await llmStep.ExecuteAsync(context, CancellationToken.None);

    // Assert
    Assert.True(result.ShouldContinue);
    Assert.Equal(42, context.Get<int>("result"));
  }

  [Fact]
  public async Task LlmStep_Should_Work_In_Full_Pipeline()
  {
    // Arrange
    var provider = new FakeLlmProvider("Paris");

    var setupStep = new TransformStep("Setup", ctx =>
    {
      ctx.Set("country", "France");
      return Task.CompletedTask;
    });

    var llmStep = new LlmStep<string>(
      name: "AskCapital",
      provider: provider,
      inputMapper: ctx => new Dictionary<string, string>
      {
        ["country"] = ctx.Get<string>("country")
      },
      promptTemplate: "What is the capital of {country}?",
      outputParser: raw => raw.Trim(),
      outputKey: "capital");

    var verifyStep = new TransformStep("Verify", ctx =>
    {
      var capital = ctx.Get<string>("capital");
      ctx.Set("verified", capital.Length > 0);
      return Task.CompletedTask;
    });

    var pipeline = new PipelineBuilder()
      .With(setupStep)
      .With(llmStep)
      .With(verifyStep)
      .Build();

    var runner = new PipelineRunner();

    // Act
    var context = await runner.RunAsync(pipeline);

    // Assert
    Assert.Equal("Paris", context.Get<string>("capital"));
    Assert.True(context.Get<bool>("verified"));
    Assert.Equal(3, context.Executions.Count);
    Assert.All(context.Executions, e => Assert.True(e.Success));
  }
}
