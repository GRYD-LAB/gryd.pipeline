namespace Gryd.Pipeline.Llm;

/// <summary>
/// Raw response from an LLM provider.
/// Contains the unprocessed output from the model.
/// </summary>
public sealed class LlmRawResponse
{
  /// <summary>
  /// The raw text content returned by the LLM.
  /// </summary>
  public required string Content { get; init; }

  /// <summary>
  /// Optional metadata about the response (e.g., token usage, model used).
  /// </summary>
  public IDictionary<string, object>? Metadata { get; init; }
}
