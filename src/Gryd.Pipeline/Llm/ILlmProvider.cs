namespace Gryd.Pipeline.Llm;

/// <summary>
/// Abstraction for an LLM provider.
/// Providers are responsible only for transport and model execution.
/// </summary>
public interface ILlmProvider
{
  /// <summary>
  /// Executes a prompt against a language model and returns raw output.
  /// </summary>
  Task<LlmRawResponse> GenerateAsync(
    LlmRequest request,
    CancellationToken ct
  );
}
