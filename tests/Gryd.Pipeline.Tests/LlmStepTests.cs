namespace Gryd.Pipeline.Tests;

using Llm;
using Steps;
using Fakes;
using Microsoft.Extensions.Options;
using System.Text.Json;

public class LlmStepTests
{
  [Fact]
  public async Task LlmStep_Should_Render_Prompt_And_Store_Result()
  {
    // Arrange
    var provider = new FakeLlmProvider("Generated response");

    var llmStep = new TestLlmStep(
      provider,
      Options.Create<LlmStepOptions>(new TestLlmStepOptions { Model = "test-model" }),
      new JsonSerializerOptions(),
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

    var llmStep = new TestLlmStepInt(
      provider,
      Options.Create<LlmStepOptions>(new TestLlmStepOptions { Model = "test-model" }),
      new JsonSerializerOptions(),
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

    var setupStep = new SimpleTransformStep("Setup", ctx => { ctx.Set("country", "France"); });

    var llmStep = new TestLlmStep(
      provider,
      Options.Create<LlmStepOptions>(new TestLlmStepOptions { Model = "test-model" }),
      new JsonSerializerOptions(),
      inputMapper: ctx => new Dictionary<string, string>
      {
        ["country"] = ctx.Get<string>("country")
      },
      promptTemplate: "What is the capital of {country}?",
      outputParser: raw => raw.Trim(),
      outputKey: "capital");

    var verifyStep = new SimpleTransformStep("Verify", ctx =>
    {
      var capital = ctx.Get<string>("capital");
      ctx.Set("verified", capital.Length > 0);
    });

    var pipeline = new PipelineBuilder()
      .With(setupStep)
      .With(llmStep)
      .With(verifyStep)
      .Build();

    var runner = new PipelineRunner();

    // Act
    var context = await runner.RunAsync(pipeline, CancellationToken.None);

    // Assert
    Assert.Equal("Paris", context.Get<string>("capital"));
    Assert.True(context.Get<bool>("verified"));
    Assert.Equal(3, context.Executions.Count);
    Assert.All(context.Executions, e => Assert.True(e.Success));
  }

  // Test helper classes
  private class TestLlmStep : LlmStep
  {
    private readonly Func<ExecutionPipelineContext, IDictionary<string, string>> _inputMapper;
    private readonly string _promptTemplate;
    private readonly Func<string, string> _outputParser;
    private readonly string _outputKey;

    public override string Name => "TestLlm";
    protected override string PromptTemplate => _promptTemplate;

    public TestLlmStep(
      Llm.ILlmProvider provider,
      IOptions<LlmStepOptions> options,
      JsonSerializerOptions jsonOptions,
      Func<ExecutionPipelineContext, IDictionary<string, string>> inputMapper,
      string promptTemplate,
      Func<string, string> outputParser,
      string outputKey) : base(provider, options, jsonOptions)
    {
      _inputMapper = inputMapper;
      _promptTemplate = promptTemplate;
      _outputParser = outputParser;
      _outputKey = outputKey;
    }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
      => _inputMapper(context);

    protected override void WriteResult(ExecutionPipelineContext context, string rawResult)
    {
      // Child class decides whether to parse the raw result
      var parsed = _outputParser(rawResult);
      context.Set(_outputKey, parsed);
    }
  }

  private class TestLlmStepInt : LlmStep
  {
    private readonly Func<ExecutionPipelineContext, IDictionary<string, string>> _inputMapper;
    private readonly string _promptTemplate;
    private readonly Func<string, int> _outputParser;
    private readonly string _outputKey;

    public override string Name => "MathLlm";
    protected override string PromptTemplate => _promptTemplate;

    public TestLlmStepInt(
      Llm.ILlmProvider provider,
      IOptions<LlmStepOptions> options,
      JsonSerializerOptions jsonOptions,
      Func<ExecutionPipelineContext, IDictionary<string, string>> inputMapper,
      string promptTemplate,
      Func<string, int> outputParser,
      string outputKey) : base(provider, options, jsonOptions)
    {
      _inputMapper = inputMapper;
      _promptTemplate = promptTemplate;
      _outputParser = outputParser;
      _outputKey = outputKey;
    }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
      => _inputMapper(context);

    protected override void WriteResult(ExecutionPipelineContext context, string rawResult)
    {
      // Child class decides whether to parse the raw result (e.g., to int)
      var parsed = _outputParser(rawResult);
      context.Set(_outputKey, parsed);
    }
  }

  private record TestLlmStepOptions : LlmStepOptions;
}
