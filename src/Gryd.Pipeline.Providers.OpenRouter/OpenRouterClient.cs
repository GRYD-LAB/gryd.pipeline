using System.Net.Http.Json;

namespace Gryd.Pipeline.Providers.OpenRouter;

using System.Text.Json;
using Microsoft.Extensions.Options;

/// <summary>
/// Typed HTTP client for OpenRouter API.
/// </summary>
public sealed class OpenRouterClient
{
  private readonly HttpClient _httpClient;
  private readonly OpenRouterOptions _options;

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
  };

  /// <summary>
  /// Initializes a new instance of the <see cref="OpenRouterClient"/> class.
  /// </summary>
  /// <param name="httpClient">The HTTP client instance.</param>
  /// <param name="options">The OpenRouter configuration options.</param>
  public OpenRouterClient(
    HttpClient httpClient,
    IOptions<OpenRouterOptions> options)
  {
    _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    // Set Authorization header
    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");

    // Set optional OpenRouter headers
    if (!string.IsNullOrWhiteSpace(_options.AppName))
    {
      _httpClient.DefaultRequestHeaders.Add("X-Title", _options.AppName);
    }

    if (!string.IsNullOrWhiteSpace(_options.Referer))
    {
      _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _options.Referer);
    }
  }

  /// <summary>
  /// Calls the OpenRouter chat completion API.
  /// </summary>
  /// <param name="request">The request payload.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <returns>The API response.</returns>
  /// <exception cref="HttpRequestException">Thrown when the API call fails.</exception>
  public async Task<OpenRouterResponse> CreateChatCompletionAsync(
    OpenRouterRequest request,
    CancellationToken ct)
  {
    var response = await _httpClient.PostAsJsonAsync(
      "chat/completions",
      request,
      JsonOptions,
      ct);

    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(
      JsonOptions,
      ct);

    if (result is null)
    {
      throw new HttpRequestException("Failed to deserialize OpenRouter response.");
    }

    return result;
  }
}
