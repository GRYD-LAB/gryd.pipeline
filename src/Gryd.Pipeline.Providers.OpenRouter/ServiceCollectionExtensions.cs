namespace Gryd.Pipeline.Providers.OpenRouter;

using Llm;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering OpenRouter provider with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Registers the OpenRouter LLM provider with the service collection.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configure">Configuration action for OpenRouter options.</param>
  /// <returns>The service collection for chaining.</returns>
  public static IServiceCollection AddOpenRouterProvider(
    this IServiceCollection services,
    Action<OpenRouterOptions> configure)
  {
    if (services is null)
    {
      throw new ArgumentNullException(nameof(services));
    }

    if (configure is null)
    {
      throw new ArgumentNullException(nameof(configure));
    }

    // Configure OpenRouterOptions
    services.Configure(configure);

    // Register typed HttpClient
    services.AddHttpClient<OpenRouterClient>(client =>
    {
      client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
    });

    // Register OpenRouterProvider as ILlmProvider
    services.AddSingleton<ILlmProvider, OpenRouterProvider>();

    return services;
  }
}
