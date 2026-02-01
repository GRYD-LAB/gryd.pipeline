using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gryd.Pipeline.Steps;

using Llm;

/// <summary>
/// Pipeline step that invokes an LLM using a prompt template
/// and stores its result explicitly in the execution context.
/// </summary>
public abstract class LlmStep<TOutput> : IPipelineStep
{
  protected readonly JsonSerializerOptions JsonOptions;

  /// <summary>
  /// Logical name of the step, used for observability and debugging.
  /// </summary>
  public abstract string Name { get; }

  /// <summary>
  /// The LLM provider to use for generation.
  /// </summary>
  public ILlmProvider Provider { get; }

  /// <summary>
  /// The prompt template to render.
  /// </summary>
  protected abstract string PromptTemplate { get; }

  /// <summary>
  /// Optional model identifier to use for this step.
  /// </summary>
  public LlmStepOptions Options { get; }

  protected LlmStep(
    ILlmProvider provider,
    IOptions<LlmStepOptions> options,
    JsonSerializerOptions jsonOptions)
  {
    JsonOptions = jsonOptions;
    Provider = provider;
    Options = options.Value;
  }

  public async Task<StepResult> ExecuteAsync(
    ExecutionPipelineContext context,
    CancellationToken ct
  )
  {
    // The step decides whether to perform work
    if (ShouldExecute(context))
    {
      // 1. Read inputs from context
      var inputs = MapInputs(context);

      // 2. Render prompt
      var prompt = RenderPrompt(PromptTemplate, inputs);

      // 3. Call provider
      var request = new LlmRequest
      {
        Prompt = prompt,
        Model = Options.Model,
        Temperature = Options.Temperature,
        MaxTokens = Options.MaxTokens
      };

      var response = await Provider.GenerateAsync(request, ct);

      // 4. Parse / validate
      var parsedOutput = Parse(response.Content);

      // 5. Store result (always)
      WriteResult(context, parsedOutput);
    }

    // 6. Decide whether pipeline should continue (independent of execution)
    return ShouldContinue(context)
      ? StepResult.Continue()
      : StepResult.Stop();
  }

  /// <summary>
  /// Maps data from the execution ctx into prompt variables.
  /// </summary>
  protected abstract IDictionary<string, string> MapInputs(
    ExecutionPipelineContext ctx);

  /// <summary>
  /// Parses the raw LLM output into the desired output type.
  /// </summary>
  /// <param name="raw"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException"></exception>
  protected virtual TOutput Parse(string raw) =>
    JsonSerializer.Deserialize<TOutput>(raw, JsonOptions)
    ?? throw new InvalidOperationException("Invalid generation output");

  /// <summary>
  /// Stores the result in the execution ctx.
  /// </summary>
  protected abstract void WriteResult(
    ExecutionPipelineContext ctx,
    TOutput result);

  /// <summary>
  /// Predicate to determine if this step should execute.
  /// If false, the step returns StepResult.Continue() without doing work.
  /// </summary>
  protected virtual bool ShouldExecute(
    ExecutionPipelineContext context) => true;

  /// <summary>
  /// Function to determine the flow control decision after execution.
  /// Receives the context and returns whether to continue (true) or stop (false).
  /// </summary>
  protected virtual bool ShouldContinue(
    ExecutionPipelineContext context) => true;

  protected static string RenderPrompt(
    string template,
    IDictionary<string, string> variables)
  {
    var result = template;
    foreach (var (key, value) in variables)
      result = result.Replace($"{{{key}}}", value);

    return result;
  }
}

public abstract record LlmStepOptions
{
  public required string Model { get; set; }
  public double Temperature { get; init; } = 0.0;
  public int? MaxTokens { get; init; }
}
