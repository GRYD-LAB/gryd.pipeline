# Gryd.Pipeline

A linear execution pipeline framework for .NET 10 designed for general-purpose orchestration of sequential operations.

## What is Gryd.Pipeline?

Gryd.Pipeline is a **general-purpose orchestration engine** that executes a fixed sequence of steps in order. It provides:

- A **linear execution model** where steps run exactly once, in order
- A **shared context** (blackboard pattern) for explicit data flow between steps
- A **domain-agnostic runtime** with no knowledge of business logic
- **Full observability** of execution timing, status, and metadata

This library can be used for:
- LLM-based processing pipelines
- RAG (Retrieval-Augmented Generation) workflows
- ETL-like data transformations
- Data enrichment and validation
- Multi-step automation workflows
- Decision pipelines with external system integration

### Domain Neutrality

This library has **no knowledge of any domain**. It does not model:
- Conversations or chat history
- Agents or dialogue systems
- Conversational state or turn-taking

Any domain-specific meaning is introduced exclusively by user-defined steps and the data they exchange through the shared context.

## Examples

Comprehensive, compilable examples are available in the [src/Gryd.Pipeline/Examples](src/Gryd.Pipeline/Examples) directory:

- **[BasicPipelineExamples.cs](src/Gryd.Pipeline/Examples/BasicPipelineExamples.cs)** - Core concepts, transformations, observability, and composition
- **[ExternalCallExamples.cs](src/Gryd.Pipeline/Examples/ExternalCallExamples.cs)** - External API integration and chained calls
- **[LlmStepExamples.cs](src/Gryd.Pipeline/Examples/LlmStepExamples.cs)** - LLM integration, prompt templating, and RAG pipelines
- **[CustomStepExamples.cs](src/Gryd.Pipeline/Examples/CustomStepExamples.cs)** - Custom step implementations (validation, retry, logging)

These examples are written in C# and compile with the project, ensuring they stay up-to-date with API changes.

## Core Abstractions

### Pipeline

A read-only collection of steps to be executed in sequence. Created via `PipelineBuilder`.

### PipelineRunner

The execution engine. Runs steps sequentially, collecting timing and status information.

```csharp
var runner = new PipelineRunner();
var context = await runner.RunAsync(pipeline);
```

### IPipelineStep

The contract for a single unit of work:

```csharp
public interface IPipelineStep
{
    string Name { get; }
    Task<StepResult> ExecuteAsync(
        PipelineExecutionContext context,
        CancellationToken cancellationToken);
}
```

Steps can:
- Read data from the context
- Write data to the context
- Call external systems
- Invoke LLMs
- Transform data
- Return `StepResult.Continue()` or `StepResult.Stop()`

### PipelineExecutionContext

A shared, mutable key-value store that acts as a blackboard for data exchange:

```csharp
var context = new PipelineExecutionContext();
context.Set("input_data", someValue);
var data = context.Get<DataType>("input_data");
```

The context also tracks execution metadata for each step (`Executions` property).

### StepResult

Flow control primitive:
- `StepResult.Continue()` — Continue to next step
- `StepResult.Stop()` — Halt execution immediately

## Execution Model & Invariants

The pipeline enforces strict execution constraints:

1. **Steps execute exactly once, in order**
   - No branching, conditional paths, or dynamic routing
   - No parallel execution or DAG-based workflows

2. **No automatic retries**
   - If a step throws an exception, execution stops
   - Retry logic must be implemented by steps or external orchestration

3. **No built-in persistence**
   - Execution context exists only in memory
   - No automatic state snapshots or checkpointing

4. **Flow control only via StepResult**
   - Steps can stop execution by returning `StepResult.Stop()`
   - No other control flow mechanisms

### Why These Constraints?

These limitations are **intentional design choices** that provide:

- **Predictability**: Execution order is always known statically
- **Testability**: Each step can be tested in complete isolation
- **Debuggability**: No hidden state or dynamic dispatch
- **Simplicity**: Easy to reason about, explain, and maintain

Complex workflows can be achieved through:
- Pipeline composition (chaining multiple pipelines)
- External orchestration layers
- Custom step implementations

## Built-In Step Types

### TransformStep

In-memory data transformation:

```csharp
var step = new TransformStep(
    name: "EnrichData",
    action: ctx =>
    {
        var input = ctx.Get<string>("raw_data");
        var enriched = ProcessData(input);
        ctx.Set("enriched_data", enriched);
        return Task.CompletedTask;
    });
```

### ExternalCallStep

Integration with external systems:

```csharp
var step = new ExternalCallStep<ApiRequest, ApiResponse>(
    name: "CallExternalApi",
    inputMapper: ctx => new ApiRequest
    {
        Query = ctx.Get<string>("query_text")
    },
    callAsync: async (input, ct) => await externalApi.QueryAsync(input, ct),
    outputMapper: (ctx, response) =>
    {
        ctx.Set("api_result", response);
    });
```

### LlmStep

LLM provider invocation with prompt templating:

```csharp
var step = new LlmStep<string>(
    name: "GenerateOutput",
    provider: llmProvider,
    inputMapper: ctx => new Dictionary<string, string>
    {
        ["input_text"] = ctx.Get<string>("input_text")
    },
    promptTemplate: "Process this data: {input_text}",
    outputParser: response => response.Trim(),
    outputKey: "generated_output",
    model: "gpt-4",
    temperature: 0.7);
```

## Context Usage Guidelines

The execution context is a shared, explicit data store with no schema or structure enforced by the framework.

### Key Design

Keys form **implicit contracts** between steps. The pipeline engine does not interpret or validate data — it only provides storage and retrieval.

**Recommended practices:**

1. **Use namespaced keys** to avoid collisions:
   ```csharp
   ctx.Set("rag.query", query);
   ctx.Set("rag.documents", docs);
   ctx.Set("llm.response", response);
   ```

2. **Centralize key definitions** where possible:
   ```csharp
   public static class ContextKeys
   {
       public const string InputData = "pipeline.input";
       public const string ProcessedData = "pipeline.processed";
   }
   ```

3. **Document contracts** between steps:
   ```csharp
   // Step 1 produces: "etl.batchId" (int)
   // Step 2 requires: "etl.batchId" (int)
   // Step 2 produces: "etl.records" (List<Record>)
   ```

### Type Safety

The `Get<T>()` method throws if:
- The key does not exist
- The stored value cannot be cast to `T`

This is **intentional** — fail fast when contracts are violated.

## Example Usage

### Simple Data Pipeline

```csharp
var validateStep = new TransformStep("Validate", ctx =>
{
    var input = ctx.Get<string>("input_data");
    if (string.IsNullOrEmpty(input))
    {
        return Task.FromResult(StepResult.Stop());
    }
    return Task.CompletedTask;
});

var enrichStep = new TransformStep("Enrich", ctx =>
{
    var input = ctx.Get<string>("input_data");
    var enriched = input.ToUpper(); // Simple transformation
    ctx.Set("enriched_data", enriched);
    return Task.CompletedTask;
});

var pipeline = new PipelineBuilder()
    .With(validateStep)
    .With(enrichStep)
    .Build();

var runner = new PipelineRunner();
var context = new PipelineExecutionContext();
context.Set("input_data", "test");

await runner.RunAsync(pipeline, context);

var result = context.Get<string>("enriched_data");
```

### LLM Enrichment Pipeline

```csharp
var prepareStep = new TransformStep("PrepareInput", ctx =>
{
    var rawData = ctx.Get<string>("raw_input");
    ctx.Set("processed_input", rawData.Trim());
    return Task.CompletedTask;
});

var llmStep = new LlmStep<string>(
    name: "EnrichWithLlm",
    provider: llmProvider,
    inputMapper: ctx => new Dictionary<string, string>
    {
        ["data"] = ctx.Get<string>("processed_input")
    },
    promptTemplate: "Analyze this data and extract key information: {data}",
    outputParser: response => response.Trim(),
    outputKey: "llm_analysis");

var storeStep = new TransformStep("Store", ctx =>
{
    var analysis = ctx.Get<string>("llm_analysis");
    // Store to database, file, etc.
    return Task.CompletedTask;
});

var pipeline = new PipelineBuilder()
    .With(prepareStep)
    .With(llmStep)
    .With(storeStep)
    .Build();
```

**For more examples**, see the [Examples directory](src/Gryd.Pipeline/Examples) which includes:
- External API integration patterns
- RAG-style pipelines with document retrieval
- Custom step implementations (retry, validation, logging)
- Multi-step LLM pipelines

## What This Library Does NOT Do

The following are **explicit non-goals**:

### No Domain Modeling
- No business logic interpretation
- No domain-specific types or abstractions
- No semantic understanding of data

### No Workflow Branching
- No conditional paths (if/else)
- No loops or iteration
- No dynamic step selection
- No parallel execution
- No DAG-based workflows

### No Fault Tolerance
- No automatic retries
- No circuit breakers
- No fallback strategies
- No dead letter queues

### No Persistence
- No state serialization
- No checkpoint/resume capability
- No execution history storage

### No Security Features
- No prompt sanitization
- No prompt injection protection
- No input validation
- No rate limiting

### No LLM-Specific Features
- No prompt engineering helpers
- No response validation
- No model selection logic
- No token management

These concerns are the responsibility of:
- User-defined steps
- External orchestration layers
- Domain-specific frameworks built on top of this library

## LLM Integration

The framework provides a provider-agnostic abstraction for LLM integration via `ILlmProvider`:

```csharp
public interface ILlmProvider
{
    Task<LlmRawResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken);
}
```

### LlmRequest

Contains:
- `Prompt` (string) — Fully rendered prompt
- `Model` (string?) — Optional model identifier
- `Temperature` (double?) — Optional temperature parameter
- `MaxTokens` (int?) — Optional token limit
- `AdditionalParameters` (IDictionary?) — Provider-specific options

### LlmRawResponse

Contains:
- `Content` (string) — Raw output text
- `Metadata` (IDictionary?) — Optional metadata (e.g., token usage)

### Provider Responsibilities

An `ILlmProvider` implementation:
- Receives a fully rendered prompt (no templating)
- Calls the remote LLM API
- Returns raw output and optional metadata
- **Does not** retry, validate, or interpret responses

## Observability

Every execution is recorded with timing and status:

```csharp
var context = await runner.RunAsync(pipeline);

foreach (var execution in context.Executions)
{
    Console.WriteLine($"{execution.StepName}: {execution.Success}");
    Console.WriteLine($"Duration: {execution.FinishedAt - execution.StartedAt}");
}
```

## Testing

The framework is designed for isolated testing:

```csharp
[Fact]
public async Task Should_Execute_Steps_In_Order()
{
    // Arrange
    var executionOrder = new List<string>();

    var step1 = new TransformStep("Step1", ctx =>
    {
        executionOrder.Add("Step1");
        return Task.CompletedTask;
    });

    var step2 = new TransformStep("Step2", ctx =>
    {
        executionOrder.Add("Step2");
        return Task.CompletedTask;
    });

    var pipeline = new PipelineBuilder()
        .With(step1)
        .With(step2)
        .Build();

    var runner = new PipelineRunner();

    // Act
    await runner.RunAsync(pipeline);

    // Assert
    Assert.Equal(new[] { "Step1", "Step2" }, executionOrder);
}
```

### Testing with Fake Providers

For LLM steps, use fake providers in tests:

```csharp
public class FakeLlmProvider : ILlmProvider
{
    private readonly Func<string, string> _responseFunc;

    public FakeLlmProvider(Func<string, string> responseFunc)
    {
        _responseFunc = responseFunc;
    }

    public Task<LlmRawResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new LlmRawResponse
        {
            Content = _responseFunc(request.Prompt)
        });
    }
}
```

## Architecture

The framework follows a clean architecture:

```
Gryd.Pipeline/
├── PipelineExecutionContext.cs   # Shared state
├── StepExecution.cs               # Telemetry
├── StepResult.cs                  # Flow control
├── IPipelineStep.cs               # Step contract
├── Pipeline.cs                    # Pipeline definition
├── PipelineBuilder.cs             # Fluent builder
├── PipelineRunner.cs              # Execution engine
├── Llm/
│   ├── ILlmProvider.cs           # Provider abstraction
│   ├── LlmRequest.cs             # Request model
│   └── LlmRawResponse.cs         # Response model
└── Steps/
    ├── LlmStep.cs                # LLM step
    ├── TransformStep.cs          # Transform step
    └── ExternalCallStep.cs       # External call step
```

## Design Rationale

### Why Linear Execution?

Linear execution provides:
- **Simplicity**: Easy to understand and explain
- **Predictability**: Execution order is statically known
- **Testability**: Each step is independently testable
- **Debuggability**: No hidden control flow or dynamic dispatch

Complex workflows can be achieved through composition or external orchestration.

### Why Explicit Data Flow?

The shared context pattern provides:
- **No magic**: All data dependencies are explicit
- **No hidden state**: Everything is in the context
- **Type safety**: Generic `Get<T>()` with runtime checks
- **Inspectability**: Context can be examined at any point

### Why Provider-Agnostic?

Abstracting LLM providers allows:
- **Flexibility**: Use any LLM API or service
- **Testability**: Mock providers for testing
- **No vendor lock-in**: Switch providers without changing pipeline logic
- **Separation of concerns**: Transport is decoupled from orchestration

### Why No Built-In Retries?

Retry logic is **intentionally omitted** because:
- Retry policies are domain-specific (backoff, limits, conditions)
- Steps should handle their own error recovery when needed
- External orchestration layers can implement retry at pipeline level
- Keeps the core engine simple and predictable

## Contributing

When extending the framework, follow these principles:
- Keep the core engine domain-agnostic
- No business logic in framework code
- Maintain linear execution model
- Prefer explicit over implicit
- Design for testability

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.
