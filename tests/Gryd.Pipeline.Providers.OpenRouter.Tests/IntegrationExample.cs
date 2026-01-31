namespace Gryd.Pipeline.Providers.OpenRouter.Tests;

using Steps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

/// <summary>
/// Integration example showing OpenRouter provider with a pipeline.
/// </summary>
public class IntegrationExample
{
  [Fact]
  public async Task Complete_Pipeline_With_OpenRouter_Provider()
  {
    // ============================================================
    // SETUP: Configure DI with OpenRouter
    // ============================================================
    var services = new ServiceCollection();

    // Register OpenRouter provider with DI
    // In production, use: Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
    services.AddOpenRouterProvider(options =>
    {
      options.ApiKey = "test-key-for-demo";
      options.AppName = "CustomerSupportApp";
      options.Referer = "https://example.com";
    });

    // For this test, we override with a fake handler
    var fakeResponse = System.Text.Json.JsonSerializer.Serialize(new
    {
      id = "test-response",
      choices = new[]
      {
        new
        {
          message = new
          {
            role = "assistant",
            content = "Thank you for being a valued Premium customer! " +
                      "To return a product, please visit our returns portal."
          },
          finish_reason = "stop"
        }
      },
      usage = new
      {
        prompt_tokens = 50,
        completion_tokens = 25,
        total_tokens = 75
      }
    });

    services.AddHttpClient<OpenRouterClient>()
      .ConfigurePrimaryHttpMessageHandler(() =>
        new FakeHttpMessageHandler(
          System.Net.HttpStatusCode.OK,
          fakeResponse));

    var serviceProvider = services.BuildServiceProvider();
    var provider = serviceProvider.GetRequiredService<Llm.ILlmProvider>();

    // ============================================================
    // BUILD PIPELINE WITH LLM STEP
    // ============================================================
    var setupStep = new TransformStep("Setup", ctx =>
    {
      ctx.Set("customer_name", "Alice Johnson");
      ctx.Set("customer_tier", "Premium");
      ctx.Set("query", "How do I return a product?");
      return Task.CompletedTask;
    });

    var llmStep = new OpenRouterResponseStep(
      provider,
      Options.Create<LlmStepOptions>(new OpenRouterLlmStepOptions
      {
        Model = "openai/gpt-4",
        Temperature = 0.7,
        MaxTokens = 500
      }),
      new JsonSerializerOptions());

    var validateStep = new TransformStep("Validate", ctx =>
    {
      var response = ctx.Get<string>("llm_response");
      ctx.Set("response_valid", !string.IsNullOrWhiteSpace(response));
      return Task.CompletedTask;
    });

    // ============================================================
    // EXECUTE PIPELINE
    // ============================================================
    var runner = new PipelineRunner();
    var pipeline = new PipelineBuilder()
      .With(setupStep)
      .With(llmStep)
      .With(validateStep)
      .Build();

    var context = await runner.RunAsync(pipeline, CancellationToken.None);

    // ============================================================
    // VERIFY RESULTS
    // ============================================================
    Assert.Equal(3, context.Executions.Count);
    Assert.All(context.Executions, e => Assert.True(e.Success));

    var llmResponse = context.Get<string>("llm_response");
    Assert.NotNull(llmResponse);
    Assert.Contains("Premium customer", llmResponse);
    Assert.Contains("return a product", llmResponse);

    Assert.True(context.Get<bool>("response_valid"));
  }

  [Fact]
  public async Task Multiple_LLM_Calls_In_Pipeline()
  {
    // ============================================================
    // SCENARIO: Multi-step reasoning pipeline
    // ============================================================
    var services = new ServiceCollection();

    services.AddOpenRouterProvider(options => { options.ApiKey = "test-key"; });

    // Mock responses for each LLM call
    var responses = new[]
    {
      "The customer wants to return a product.",
      "Provide the returns portal URL and explain the 30-day policy."
    };

    var handler = new CallCountingFakeHandler(responses);

    services.AddHttpClient<OpenRouterClient>()
      .ConfigurePrimaryHttpMessageHandler(() => handler);

    var serviceProvider = services.BuildServiceProvider();
    var provider = serviceProvider.GetRequiredService<Llm.ILlmProvider>();

    // Step 1: Classify intent
    var classifyStep = new ClassifyIntentOpenRouterStep(
      provider,
      Options.Create<LlmStepOptions>(new OpenRouterLlmStepOptions { Model = "openai/gpt-3.5-turbo" }),
      new JsonSerializerOptions());

    // Step 2: Generate response based on intent
    var respondStep = new GenerateResponseOpenRouterStep(
      provider,
      Options.Create<LlmStepOptions>(new OpenRouterLlmStepOptions { Model = "openai/gpt-4" }),
      new JsonSerializerOptions());

    var runner = new PipelineRunner();
    var pipeline = new PipelineBuilder()
      .With(classifyStep)
      .With(respondStep)
      .Build();

    var context = new ExecutionPipelineContext();
    context.Set("query", "I need to return my order");

    await runner.RunAsync(pipeline, context, CancellationToken.None);

    // Verify both LLM steps executed
    Assert.Equal(2, context.Executions.Count);
    Assert.Equal("ClassifyIntent", context.Executions[0].StepName);
    Assert.Equal("GenerateResponse", context.Executions[1].StepName);

    var intent = context.Get<string>("intent");
    var finalResponse = context.Get<string>("final_response");

    Assert.Contains("return", intent);
    Assert.Contains("returns portal", finalResponse);

    // Verify provider was called twice
    Assert.Equal(2, handler.CallCount);
  }
}

/// <summary>
/// Fake handler that returns different responses based on call count.
/// </summary>
internal class CallCountingFakeHandler : HttpMessageHandler
{
  private readonly string[] _responses;
  private int _callCount;

  public int CallCount => _callCount;

  public CallCountingFakeHandler(string[] responses)
  {
    _responses = responses;
    _callCount = 0;
  }

  protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    var index = _callCount;
    _callCount++;

    var responseText = index < _responses.Length
      ? _responses[index]
      : "Default response";

    var fakeResponse = System.Text.Json.JsonSerializer.Serialize(new
    {
      id = $"response-{index}",
      choices = new[]
      {
        new
        {
          message = new { role = "assistant", content = responseText },
          finish_reason = "stop"
        }
      },
      usage = new
      {
        prompt_tokens = 10,
        completion_tokens = 5,
        total_tokens = 15
      }
    });

    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
    {
      Content = new StringContent(
        fakeResponse,
        System.Text.Encoding.UTF8,
        "application/json")
    };

    return Task.FromResult(response);
  }
}

/// <summary>
/// Fake HTTP handler for testing.
/// </summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
  private readonly System.Net.HttpStatusCode _statusCode;
  private readonly string _responseContent;

  public FakeHttpMessageHandler(System.Net.HttpStatusCode statusCode, string responseContent)
  {
    _statusCode = statusCode;
    _responseContent = responseContent;
  }

  protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    var response = new HttpResponseMessage(_statusCode)
    {
      Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
    };
    return Task.FromResult(response);
  }
}

/// <summary>
/// Concrete LlmStep implementations for OpenRouter tests
/// </summary>
internal class OpenRouterResponseStep : Steps.LlmStep<string>
{
  public override string Name => "GenerateResponse";

  protected override string PromptTemplate => @"
Customer: {customer_name} ({customer_tier} tier)
Query: {query}

Generate a helpful and personalized response:";

  public OpenRouterResponseStep(
    Llm.ILlmProvider provider,
    IOptions<Steps.LlmStepOptions> options,
    JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
  {
  }

  protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
  {
    return new Dictionary<string, string>
    {
      ["customer_name"] = context.Get<string>("customer_name"),
      ["customer_tier"] = context.Get<string>("customer_tier"),
      ["query"] = context.Get<string>("query")
    };
  }

  protected override string Parse(string raw) => raw.Trim();

  protected override void WriteResult(ExecutionPipelineContext context, string result)
  {
    context.Set("llm_response", result);
  }
}

internal class ClassifyIntentOpenRouterStep : Steps.LlmStep<string>
{
  public override string Name => "ClassifyIntent";
  protected override string PromptTemplate => "Classify the intent of: {query}";

  public ClassifyIntentOpenRouterStep(
    Llm.ILlmProvider provider,
    IOptions<Steps.LlmStepOptions> options,
    JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
  {
  }

  protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
  {
    return new Dictionary<string, string>
    {
      ["query"] = context.Get<string>("query")
    };
  }

  protected override string Parse(string raw) => raw.Trim();

  protected override void WriteResult(ExecutionPipelineContext context, string result)
  {
    context.Set("intent", result);
  }
}

internal class GenerateResponseOpenRouterStep : Steps.LlmStep<string>
{
  public override string Name => "GenerateResponse";
  protected override string PromptTemplate => "Intent: {intent}\nQuery: {query}\n\nGenerate response:";

  public GenerateResponseOpenRouterStep(
    Llm.ILlmProvider provider,
    IOptions<Steps.LlmStepOptions> options,
    JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
  {
  }

  protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
  {
    return new Dictionary<string, string>
    {
      ["intent"] = context.Get<string>("intent"),
      ["query"] = context.Get<string>("query")
    };
  }

  protected override string Parse(string raw) => raw.Trim();

  protected override void WriteResult(ExecutionPipelineContext context, string result)
  {
    context.Set("final_response", result);
  }
}

internal record OpenRouterLlmStepOptions : Steps.LlmStepOptions;
