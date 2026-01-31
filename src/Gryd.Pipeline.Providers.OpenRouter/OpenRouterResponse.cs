namespace Gryd.Pipeline.Providers.OpenRouter;

using System.Text.Json.Serialization;

/// <summary>
/// Internal DTO for OpenRouter API response.
/// </summary>
public sealed class OpenRouterResponse
{
  [JsonPropertyName("id")] public string? Id { get; init; }

  [JsonPropertyName("choices")] public required List<Choice> Choices { get; init; }

  [JsonPropertyName("usage")] public Usage? UsageData { get; init; }

  public sealed class Choice
  {
    [JsonPropertyName("message")] public required Message MessageData { get; init; }

    [JsonPropertyName("finish_reason")] public string? FinishReason { get; init; }
  }

  public sealed class Message
  {
    [JsonPropertyName("role")] public string? Role { get; init; }

    [JsonPropertyName("content")] public required string Content { get; init; }
  }

  public sealed class Usage
  {
    [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")] public int TotalTokens { get; init; }
  }
}
