# Gryd.Pipeline.Providers.OpenRouter

A simple, production-ready OpenRouter LLM provider for the Gryd.Pipeline framework.

## What This Provider Is

This provider is a **transport adapter** that bridges Gryd.Pipeline and the OpenRouter API. It provides a generic way to
invoke OpenRouter models from any pipeline workflow.

This provider has **no knowledge of**:

- Conversations or chat history
- Pipeline semantics or execution context
- Domain logic or business rules
- Multi-turn interactions

It is a stateless HTTP client wrapper that:

- Accepts a fully rendered prompt string
- Sends it to OpenRouter
- Returns raw output text and token usage metadata

## Responsibilities vs Non-Responsibilities

### This Provider DOES:

- Accept fully rendered prompts (plain strings)
- Send HTTP POST requests to OpenRouter API
- Serialize requests to JSON
- Deserialize responses from JSON
- Return raw output text
- Return token usage metadata
- Set API key and optional headers
- Throw exceptions on HTTP errors

### This Provider DOES NOT:

- Retry failed requests
- Interpret or validate responses
- Parse structured output
- Modify or sanitize prompts
- Control pipeline execution
- Apply domain logic
- Manage conversation state
- Store history
- Implement rate limiting
- Implement circuit breakers

## Installation

Add the project reference to your pipeline application:

```xml
<ProjectReference Include="..\Gryd.Pipeline.Providers.OpenRouter\Gryd.Pipeline.Providers.OpenRouter.csproj" />
```

## Registration with Dependency Injection

Register the provider with your service collection:

```csharp
using Gryd.Pipeline.Providers.OpenRouter;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddOpenRouterProvider(options =>
{
    options.ApiKey = "your-openrouter-api-key";
    options.AppName = "MyApp";           // Optional
    options.Referer = "https://myapp.com"; // Optional
});

var serviceProvider = services.BuildServiceProvider();
```

## Usage in Pipelines

> ⚠️ **Important**: `LlmStep<T>` is an abstract base class. You must create concrete implementations that define your specific LLM behavior. There is no direct instantiation.

```csharp
using Gryd.Pipeline;
using Gryd.Pipeline.Llm;
using Gryd.Pipeline.Steps;
using Microsoft.Extensions.DependencyInjection;

// Setup DI
var services = new ServiceCollection();
services.AddOpenRouterProvider(options =>
{
    options.ApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")!;
});

var serviceProvider = services.BuildServiceProvider();
var provider = serviceProvider.GetRequiredService<ILlmProvider>();

// Create concrete LLM step implementation
public class ProcessDataStep : LlmStep<string>
{
    public override string Name => "ProcessData";
    protected override string PromptTemplate => "Process this data: {input_data}";

    public ProcessDataStep(ILlmProvider provider, IOptions<LlmStepOptions> options, JsonSerializerOptions jsonOptions)
        : base(provider, options, jsonOptions) { }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
    {
        return new Dictionary<string, string>
        {
            ["input_data"] = context.Get<string>("raw_input")
        };
    }

    protected override string Parse(string raw) => raw.Trim();

    protected override void WriteResult(ExecutionPipelineContext context, string result)
    {
        context.Set("processed_output", result);
    }
}

// Create step with options
var llmStep = new ProcessDataStep(
    provider,
    Options.Create<LlmStepOptions>(new MyLlmOptions
    {
        Model = "openai/gpt-4",
        Temperature = 0.7
    }),
    new JsonSerializerOptions());

// Build and run pipeline
var runner = new PipelineRunner();
var pipeline = new PipelineBuilder()
    .With(llmStep)
    .Build();

var context = new ExecutionPipelineContext();
context.Set("raw_input", "example data");

await runner.RunAsync(pipeline, context, CancellationToken.None);

var output = context.Get<string>("processed_output");
Console.WriteLine(output);
```

## Retry Responsibility

This provider **intentionally does not implement retries**. Retry logic belongs at one of these levels:

1. **Step level**: Wrap the LlmStep in a custom retry step
2. **Pipeline level**: External orchestration retries the entire pipeline
3. **Execution policy level**: Middleware or decorator around PipelineRunner

Rationale:

- Retry policies are domain-specific (backoff strategy, max attempts, conditions)
- Transport concerns should be decoupled from execution policy
- Keeps the provider simple, predictable, and testable
- Allows users to implement retry logic appropriate for their use case

## HttpClientFactory Integration

This provider uses `IHttpClientFactory` correctly:

- ✅ No manual `new HttpClient()`
- ✅ Proper lifetime management
- ✅ Connection pooling
- ✅ Testable via custom `HttpMessageHandler`

## Testing

To test with a fake HTTP handler:

```csharp
var services = new ServiceCollection();

services.AddOpenRouterProvider(options =>
{
    options.ApiKey = "test-key";
});

// Override HttpClient with test handler
services.AddHttpClient<OpenRouterClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new FakeHttpMessageHandler());

var serviceProvider = services.BuildServiceProvider();
var provider = serviceProvider.GetRequiredService<ILlmProvider>();

// Use provider in tests...
```

## Supported Models

Any model available on OpenRouter. Examples:

- `openai/gpt-4`
- `openai/gpt-3.5-turbo`
- `anthropic/claude-2`
- `google/palm-2-chat-bison`
- `meta-llama/llama-2-70b-chat`

See [OpenRouter documentation](https://openrouter.ai/docs) for the full list.

## Architecture

```
┌─────────────────┐
│   LlmStep       │
└────────┬────────┘
         │
         │ LlmRequest
         │
┌────────▼────────────┐
│ OpenRouterProvider  │ ◄── ILlmProvider
└────────┬────────────┘
         │
         │ OpenRouterRequest
         │
┌────────▼────────────┐
│ OpenRouterClient    │ ◄── Typed HttpClient
└────────┬────────────┘
         │
         │ HTTP POST
         │
┌────────▼────────────┐
│  OpenRouter API     │
└─────────────────────┘
```

## Design Principles

- **Single responsibility**: HTTP transport only, no business logic
- **Stateless**: No conversation tracking or history management
- **Domain-agnostic**: No assumptions about use case
- **DI-native**: Designed for dependency injection
- **Testable**: Easy to mock via HttpMessageHandler
- **Predictable**: Straightforward mapping between request and response

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](../../../LICENSE) file for details.
