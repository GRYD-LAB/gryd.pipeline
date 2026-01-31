namespace Gryd.Pipeline.Providers.OpenRouter;

using Llm;

/// <summary>
/// OpenRouter LLM provider implementation.
/// This is a simple transport adapter that sends requests to OpenRouter and returns raw responses.
/// </summary>
public sealed class OpenRouterProvider : ILlmProvider
{
  private readonly OpenRouterClient _client;

  /// <summary>
  /// Initializes a new instance of the <see cref="OpenRouterProvider"/> class.
  /// </summary>
  /// <param name="client">The OpenRouter HTTP client.</param>
  public OpenRouterProvider(OpenRouterClient client)
  {
    _client = client ?? throw new ArgumentNullException(nameof(client));
  }

  /// <summary>
  /// Generates a completion from OpenRouter.
  /// </summary>
  /// <param name="request">The LLM request containing the prompt and parameters.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>The raw LLM response with text and token usage.</returns>
  public async Task<LlmRawResponse> GenerateAsync(
    LlmRequest request,
    CancellationToken ct)
  {
    // Map LlmRequest to OpenRouterRequest
    var openRouterRequest = new OpenRouterRequest
    {
      Model = request.Model ?? throw new InvalidOperationException(),
      Messages = new List<OpenRouterRequest.Message>
      {
        new OpenRouterRequest.Message
        {
          Role = "user",
          Content = request.Prompt
        }
      },
      Temperature = request.Temperature,
      MaxTokens = request.MaxTokens
    };

    // Call OpenRouter API
    var openRouterResponse = await _client.CreateChatCompletionAsync(
      openRouterRequest,
      ct);

    // Map OpenRouterResponse to LlmRawResponse
    var firstChoice = openRouterResponse.Choices.FirstOrDefault();
    if (firstChoice is null)
    {
      throw new InvalidOperationException("OpenRouter response contained no choices.");
    }

    var content = firstChoice.MessageData.Content;

    var usage = openRouterResponse.UsageData;
    var metadata = new Dictionary<string, object>();

    if (usage is not null)
    {
      metadata["prompt_tokens"] = usage.PromptTokens;
      metadata["completion_tokens"] = usage.CompletionTokens;
      metadata["total_tokens"] = usage.TotalTokens;
    }

    return new LlmRawResponse
    {
      Content = content,
      Metadata = metadata
    };
  }
}
