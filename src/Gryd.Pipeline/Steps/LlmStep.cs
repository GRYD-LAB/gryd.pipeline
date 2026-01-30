namespace Gryd.Pipeline.Steps;

using Llm;

/// <summary>
/// Pipeline step that invokes an LLM using a prompt template
/// and stores its result explicitly in the execution context.
/// </summary>
public sealed class LlmStep<TOutput> : IPipelineStep
{
  /// <summary>
  /// Logical name of the step, used for observability and debugging.
  /// </summary>
  public string Name { get; }

  /// <summary>
  /// The LLM provider to use for generation.
  /// </summary>
  public ILlmProvider Provider { get; }

  /// <summary>
  /// Maps data from the execution context into prompt variables.
  /// </summary>
  public Func<PipelineExecutionContext, IDictionary<string, string>> InputMapper { get; }

  /// <summary>
  /// The prompt template to render.
  /// </summary>
  public string PromptTemplate { get; }

  /// <summary>
  /// Parses the raw LLM response into the desired output type.
  /// </summary>
  public Func<string, TOutput> OutputParser { get; }

  /// <summary>
  /// Key under which the parsed result will be stored in the context.
  /// </summary>
  public string OutputKey { get; }

  /// <summary>
  /// Optional model identifier.
  /// </summary>
  public string? Model { get; }

  /// <summary>
  /// Optional temperature parameter.
  /// </summary>
  public double? Temperature { get; }

  /// <summary>
  /// Optional maximum tokens.
  /// </summary>
  public int? MaxTokens { get; }

  /// <summary>
  /// Predicate to determine if this step should execute.
  /// If false, the step returns StepResult.Continue() without doing work.
  /// </summary>
  public Func<PipelineExecutionContext, bool> ExecutionCondition { get; }

  /// <summary>
  /// Function to determine the flow control decision after execution.
  /// Receives the context and returns whether to continue (true) or stop (false).
  /// </summary>
  public Func<PipelineExecutionContext, bool> ContinuationCondition { get; }

  public LlmStep(
    string name,
    ILlmProvider provider,
    Func<PipelineExecutionContext, IDictionary<string, string>> inputMapper,
    string promptTemplate,
    Func<string, TOutput> outputParser,
    string outputKey,
    string? model = null,
    double? temperature = null,
    int? maxTokens = null,
    Func<PipelineExecutionContext, bool>? executionCondition = null,
    Func<PipelineExecutionContext, bool>? continuationCondition = null)
  {
    Name = name;
    Provider = provider;
    InputMapper = inputMapper;
    PromptTemplate = promptTemplate;
    OutputParser = outputParser;
    OutputKey = outputKey;
    Model = model;
    Temperature = temperature;
    MaxTokens = maxTokens;
    ExecutionCondition = executionCondition ?? (_ => true);
    ContinuationCondition = continuationCondition ?? (_ => true);
  }

  public async Task<StepResult> ExecuteAsync(
    PipelineExecutionContext context,
    CancellationToken ct
  )
  {
    // The step decides whether to perform work
    if (ExecutionCondition(context))
    {
      // 1. Read inputs from context
      var inputs = InputMapper(context);

      // 2. Render prompt
      var prompt = RenderPrompt(PromptTemplate, inputs);

      // 3. Call provider
      var request = new LlmRequest
      {
        Prompt = prompt,
        Model = Model,
        Temperature = Temperature,
        MaxTokens = MaxTokens
      };

      var response = await Provider.GenerateAsync(request, ct);

      // 4. Parse / validate
      var parsedOutput = OutputParser(response.Content);

      // 5. Store result explicitly in context
      context.Set(OutputKey, parsedOutput);
    }

    // 6. Decide whether pipeline should continue (independent of execution)
    return ContinuationCondition(context)
      ? StepResult.Continue()
      : StepResult.Stop();
  }

  private static string RenderPrompt(string template, IDictionary<string, string> variables)
  {
    var result = template;
    foreach (var (key, value) in variables)
    {
      result = result.Replace($"{{{key}}}", value);
    }

    return result;
  }
}
