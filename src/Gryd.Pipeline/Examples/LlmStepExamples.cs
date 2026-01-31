namespace Gryd.Pipeline.Examples;

using Llm;
using Steps;
using Microsoft.Extensions.Options;
using System.Text.Json;

/// <summary>
/// Examples demonstrating LlmStep usage with different providers.
///
/// IMPORTANT: LlmStep&lt;T&gt; is an ABSTRACT base class. All examples in this file
/// demonstrate concrete implementations (BasicResponseStep, ClassifyIntentStep, etc.).
///
/// You cannot instantiate LlmStep&lt;T&gt; directly - you must create concrete subclasses
/// that implement the required abstract members:
/// - Name (property)
/// - PromptTemplate (property)
/// - MapInputs(context)
/// - WriteResult(context, result)
/// </summary>
public static class LlmStepExamples
{
  /// <summary>
  /// Basic LLM step example with prompt templating.
  /// </summary>
  public static async Task BasicLlmStepExample()
  {
    var provider = new FakeLlmProvider(prompt => $"Response to: {prompt}");

    var llmStep = new BasicResponseStep(
      provider,
      Options.Create<LlmStepOptions>(new ExampleLlmStepOptions { Model = "gpt-4", Temperature = 0.7 }),
      new JsonSerializerOptions());

    var pipeline = new PipelineBuilder()
      .With(llmStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("user_query", "What is a pipeline?");

    await runner.RunAsync(pipeline, context, CancellationToken.None);

    var response = context.Get<string>("llm_response");
    Console.WriteLine(response);
  }

  /// <summary>
  /// Multi-step LLM pipeline with data enrichment.
  /// </summary>
  public static async Task MultiStepLlmPipeline()
  {
    var provider = new FakeLlmProvider(prompt =>
      prompt.Contains("classify") ? "technical_question" : "Here is the answer");

    var prepareStep = new TransformStep("PrepareInput", ctx =>
    {
      var rawQuery = ctx.Get<string>("raw_query");
      ctx.Set("cleaned_query", rawQuery.Trim());
      return Task.CompletedTask;
    });

    var classifyStep = new ClassifyIntentStep(
      provider,
      Options.Create<LlmStepOptions>(new ExampleLlmStepOptions { Model = "gpt-3.5-turbo", Temperature = 0.3 }),
      new JsonSerializerOptions());

    var respondStep = new GenerateResponseStep(
      provider,
      Options.Create<LlmStepOptions>(new ExampleLlmStepOptions { Model = "gpt-4", Temperature = 0.7 }),
      new JsonSerializerOptions());

    var pipeline = new PipelineBuilder()
      .With(prepareStep)
      .With(classifyStep)
      .With(respondStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("raw_query", "  How do I configure the pipeline?  ");

    await runner.RunAsync(pipeline, context, CancellationToken.None);

    var intent = context.Get<string>("intent");
    var response = context.Get<string>("final_response");

    Console.WriteLine($"Intent: {intent}");
    Console.WriteLine($"Response: {response}");
  }

  /// <summary>
  /// LLM step with custom output parsing.
  /// </summary>
  public static async Task CustomOutputParsingExample()
  {
    var provider = new FakeLlmProvider(_ => "Answer: 42\nExplanation: This is the answer");

    var llmStep = new ExtractStructuredDataStep(
      provider,
      Options.Create<LlmStepOptions>(new ExampleLlmStepOptions { Model = "gpt-4" }),
      new JsonSerializerOptions());

    var pipeline = new PipelineBuilder()
      .With(llmStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("question", "What is the meaning of life?");

    await runner.RunAsync(pipeline, context, CancellationToken.None);

    var parsed = context.Get<ParsedResponse>("parsed_response");
    Console.WriteLine($"Answer: {parsed.Answer}");
    Console.WriteLine($"Explanation: {parsed.Explanation}");
  }

  /// <summary>
  /// RAG-like pipeline with document retrieval and LLM generation.
  /// </summary>
  public static async Task RagStylePipeline()
  {
    var provider = new FakeLlmProvider(prompt =>
      $"Based on the documents, here's the answer to your question.");

    var retrieveDocsStep = new ExternalCallStep<List<string>>(
      name: "RetrieveDocuments",
      call: async ctx =>
      {
        // Simulate document retrieval
        await Task.Delay(10);
        return new List<string>
        {
          "Document 1: Pipeline basics",
          "Document 2: Advanced usage",
          "Document 3: Best practices"
        };
      },
      saveResult: (ctx, docs) => ctx.Set("retrieved_docs", docs));

    var formatContextStep = new TransformStep("FormatContext", ctx =>
    {
      var docs = ctx.Get<List<string>>("retrieved_docs");
      var context = string.Join("\n", docs);
      ctx.Set("document_context", context);
      return Task.CompletedTask;
    });

    var generateStep = new GenerateAnswerStep(
      provider,
      Options.Create<LlmStepOptions>(new ExampleLlmStepOptions { Model = "gpt-4" }),
      new JsonSerializerOptions());

    var pipeline = new PipelineBuilder()
      .With(retrieveDocsStep)
      .With(formatContextStep)
      .With(generateStep)
      .Build();

    var runner = new PipelineRunner();
    var context = new ExecutionPipelineContext();
    context.Set("query", "How do I use pipelines?");

    await runner.RunAsync(pipeline, context, CancellationToken.None);

    var answer = context.Get<string>("answer");
    Console.WriteLine(answer);
  }

  // Supporting types
  public class ParsedResponse
  {
    public string Answer { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
  }

  // Fake LLM provider for examples
  private class FakeLlmProvider : ILlmProvider
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
        Content = _responseFunc(request.Prompt),
        Metadata = new Dictionary<string, object>
        {
          ["prompt_tokens"] = 10,
          ["completion_tokens"] = 20,
          ["total_tokens"] = 30
        }
      });
    }
  }

  // ============================================================================
  // CONCRETE LlmStep IMPLEMENTATIONS
  // ============================================================================
  // LlmStep<T> is ABSTRACT and cannot be instantiated directly.
  // These classes show the required pattern for creating concrete LLM steps.
  // Each implementation must provide:
  //   - Name (identifies the step)
  //   - PromptTemplate (the prompt with placeholders)
  //   - MapInputs() (reads data from context)
  //   - WriteResult() (writes data to context)
  //   - Parse() (optional: custom response parsing)
  // ============================================================================

  private class BasicResponseStep : LlmStep<string>
  {
    public override string Name => "GenerateResponse";
    protected override string PromptTemplate => "Answer this question: {query}";

    public BasicResponseStep(
      ILlmProvider provider,
      IOptions<LlmStepOptions> options,
      JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
    {
    }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
    {
      return new Dictionary<string, string>
      {
        ["query"] = context.Get<string>("user_query")
      };
    }

    protected override string Parse(string raw) => raw.Trim();

    protected override void WriteResult(ExecutionPipelineContext context, string result)
    {
      context.Set("llm_response", result);
    }
  }

  private class ClassifyIntentStep : LlmStep<string>
  {
    public override string Name => "ClassifyIntent";
    protected override string PromptTemplate => "Classify the intent of: {query}";

    public ClassifyIntentStep(
      ILlmProvider provider,
      IOptions<LlmStepOptions> options,
      JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
    {
    }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
    {
      return new Dictionary<string, string>
      {
        ["query"] = context.Get<string>("cleaned_query")
      };
    }

    protected override string Parse(string raw) => raw.Trim().ToLower();

    protected override void WriteResult(ExecutionPipelineContext context, string result)
    {
      context.Set("intent", result);
    }
  }

  private class GenerateResponseStep : LlmStep<string>
  {
    public override string Name => "GenerateResponse";
    protected override string PromptTemplate => "Intent: {intent}\nQuery: {query}\n\nProvide a response:";

    public GenerateResponseStep(
      ILlmProvider provider,
      IOptions<LlmStepOptions> options,
      JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
    {
    }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
    {
      return new Dictionary<string, string>
      {
        ["intent"] = context.Get<string>("intent"),
        ["query"] = context.Get<string>("cleaned_query")
      };
    }

    protected override string Parse(string raw) => raw.Trim();

    protected override void WriteResult(ExecutionPipelineContext context, string result)
    {
      context.Set("final_response", result);
    }
  }

  private class ExtractStructuredDataStep : LlmStep<ParsedResponse>
  {
    public override string Name => "ExtractStructuredData";
    protected override string PromptTemplate => "Question: {question}";

    public ExtractStructuredDataStep(
      ILlmProvider provider,
      IOptions<LlmStepOptions> options,
      JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
    {
    }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
    {
      return new Dictionary<string, string>
      {
        ["question"] = context.Get<string>("question")
      };
    }

    protected override ParsedResponse Parse(string raw)
    {
      var lines = raw.Split('\n');
      var answer = lines.FirstOrDefault(l => l.StartsWith("Answer:"))?.Replace("Answer:", "").Trim();
      var explanation = lines.FirstOrDefault(l => l.StartsWith("Explanation:"))?.Replace("Explanation:", "").Trim();

      return new ParsedResponse
      {
        Answer = answer ?? string.Empty,
        Explanation = explanation ?? string.Empty
      };
    }

    protected override void WriteResult(ExecutionPipelineContext context, ParsedResponse result)
    {
      context.Set("parsed_response", result);
    }
  }

  private class GenerateAnswerStep : LlmStep<string>
  {
    public override string Name => "GenerateAnswer";
    protected override string PromptTemplate => @"Context:
{context}

Question: {query}

Answer based on the context:";

    public GenerateAnswerStep(
      ILlmProvider provider,
      IOptions<LlmStepOptions> options,
      JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
    {
    }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
    {
      return new Dictionary<string, string>
      {
        ["query"] = context.Get<string>("query"),
        ["context"] = context.Get<string>("document_context")
      };
    }

    protected override string Parse(string raw) => raw.Trim();

    protected override void WriteResult(ExecutionPipelineContext context, string result)
    {
      context.Set("answer", result);
    }
  }

  // Concrete options implementation for examples
  private record ExampleLlmStepOptions : LlmStepOptions;
}
