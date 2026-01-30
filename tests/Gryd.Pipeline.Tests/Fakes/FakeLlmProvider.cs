namespace Gryd.Pipeline.Tests.Fakes;

using Llm;

/// <summary>
/// Fake LLM provider for testing purposes.
/// Returns predictable responses based on the input prompt.
/// </summary>
public class FakeLlmProvider : ILlmProvider
{
  private readonly Func<string, string>? _responseGenerator;
  private readonly string? _fixedResponse;

  public FakeLlmProvider(string fixedResponse)
  {
    _fixedResponse = fixedResponse;
  }

  public FakeLlmProvider(Func<string, string> responseGenerator)
  {
    _responseGenerator = responseGenerator;
  }

  public Task<LlmRawResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
  {
    var content = _responseGenerator != null
      ? _responseGenerator(request.Prompt)
      : _fixedResponse ?? "Default response";

    return Task.FromResult(new LlmRawResponse
    {
      Content = content,
      Metadata = new Dictionary<string, object>
      {
        ["model"] = request.Model ?? "fake-model",
        ["prompt_length"] = request.Prompt.Length
      }
    });
  }
}
