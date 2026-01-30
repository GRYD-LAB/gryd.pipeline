namespace Gryd.Pipeline.Providers.OpenRouter;

/// <summary>
/// Configuration options for the OpenRouter LLM provider.
/// </summary>
public sealed class OpenRouterOptions
{
  /// <summary>
  /// OpenRouter API key.
  /// </summary>
  public string ApiKey { get; set; } = string.Empty;

  /// <summary>
  /// Optional application name sent to OpenRouter.
  /// </summary>
  public string? AppName { get; set; }

  /// <summary>
  /// Optional referer URL sent to OpenRouter.
  /// </summary>
  public string? Referer { get; set; }
}
