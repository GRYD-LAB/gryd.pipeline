namespace Gryd.Pipeline.Llm;

/// <summary>
/// Represents a request to an LLM provider.
/// </summary>
public sealed class LlmRequest
{
  /// <summary>
  /// The prompt to send to the LLM.
  /// </summary>
  public required string Prompt { get; init; }

  /// <summary>
  /// Optional model identifier (e.g., "gpt-4", "claude-3").
  /// </summary>
  public string? Model { get; init; }

  /// <summary>
  /// Optional temperature parameter for response generation.
  /// </summary>
  public double? Temperature { get; init; }

  /// <summary>
  /// Optional maximum tokens for the response.
  /// </summary>
  public int? MaxTokens { get; init; }

  /// <summary>
  /// Additional provider-specific parameters.
  /// </summary>
  public IDictionary<string, object>? AdditionalParameters { get; init; }
}
