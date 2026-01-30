namespace Gryd.Pipeline.Providers.OpenRouter.Tests;

using Steps;
using Microsoft.Extensions.DependencyInjection;
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

    var llmStep = new LlmStep<string>(
      name: "GenerateResponse",
      provider: provider,
      inputMapper: ctx => new Dictionary<string, string>
      {
        ["customer_name"] = ctx.Get<string>("customer_name"),
        ["customer_tier"] = ctx.Get<string>("customer_tier"),
        ["query"] = ctx.Get<string>("query")
      },
      promptTemplate: @"
Customer: {customer_name} ({customer_tier} tier)
Query: {query}

Generate a helpful and personalized response:",
      outputParser: response => response.Trim(),
      outputKey: "llm_response",
      model: "openai/gpt-4",
      temperature: 0.7,
      maxTokens: 500);

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

    var context = await runner.RunAsync(pipeline);

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

    services.AddOpenRouterProvider(options =>
    {
      options.ApiKey = "test-key";
    });

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
    var classifyStep = new LlmStep<string>(
      name: "ClassifyIntent",
      provider: provider,
      inputMapper: ctx => new Dictionary<string, string>
      {
        ["query"] = ctx.Get<string>("query")
      },
      promptTemplate: "Classify the intent of: {query}",
      outputParser: response => response.Trim(),
      outputKey: "intent",
      model: "openai/gpt-3.5-turbo");

    // Step 2: Generate response based on intent
    var respondStep = new LlmStep<string>(
      name: "GenerateResponse",
      provider: provider,
      inputMapper: ctx => new Dictionary<string, string>
      {
        ["intent"] = ctx.Get<string>("intent"),
        ["query"] = ctx.Get<string>("query")
      },
      promptTemplate: "Intent: {intent}\nQuery: {query}\n\nGenerate response:",
      outputParser: response => response.Trim(),
      outputKey: "final_response",
      model: "openai/gpt-4");

    var runner = new PipelineRunner();
    var pipeline = new PipelineBuilder()
      .With(classifyStep)
      .With(respondStep)
      .Build();

    var context = new PipelineExecutionContext();
    context.Set("query", "I need to return my order");

    await runner.RunAsync(pipeline, context);

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
