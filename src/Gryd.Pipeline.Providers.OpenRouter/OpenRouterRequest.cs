namespace Gryd.Pipeline.Providers.OpenRouter;

using System.Text.Json.Serialization;

/// <summary>
/// Internal DTO for OpenRouter API request.
/// </summary>
public sealed class OpenRouterRequest
{
  [JsonPropertyName("model")] public required string Model { get; init; }

  [JsonPropertyName("messages")] public required List<Message> Messages { get; init; }

  [JsonPropertyName("temperature")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public double? Temperature { get; init; }

  [JsonPropertyName("max_tokens")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public int? MaxTokens { get; init; }

  public sealed class Message
  {
    [JsonPropertyName("role")] public required string Role { get; init; }

    [JsonPropertyName("content")] public required string Content { get; init; }
  }
}
