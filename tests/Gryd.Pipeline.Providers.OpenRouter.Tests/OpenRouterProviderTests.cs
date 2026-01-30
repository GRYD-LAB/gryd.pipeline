namespace Gryd.Pipeline.Providers.OpenRouter.Tests;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Llm;
using Xunit;

/// <summary>
/// Tests for OpenRouter provider implementation.
/// </summary>
public class OpenRouterProviderTests
{
  [Fact]
  public async Task Provider_Should_Map_Request_And_Response_Correctly()
  {
    // Arrange
    var fakeResponse = new
    {
      id = "test-123",
      choices = new[]
      {
        new
        {
          message = new { role = "assistant", content = "Hello, world!" },
          finish_reason = "stop"
        }
      },
      usage = new
      {
        prompt_tokens = 10,
        completion_tokens = 5,
        total_tokens = 15
      }
    };

    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(fakeResponse));
    var services = new ServiceCollection();

    services.AddOpenRouterProvider(options =>
    {
      options.ApiKey = "test-api-key";
    });

    services.AddHttpClient<OpenRouterClient>()
      .ConfigurePrimaryHttpMessageHandler(() => handler);

    var serviceProvider = services.BuildServiceProvider();
    var client = serviceProvider.GetRequiredService<OpenRouterClient>();
    var provider = new OpenRouterProvider(client);

    var request = new LlmRequest
    {
      Prompt = "Say hello",
      Model = "openai/gpt-4",
      Temperature = 0.7,
      MaxTokens = 100
    };

    // Act
    var response = await provider.GenerateAsync(request);

    // Assert
    Assert.Equal("Hello, world!", response.Content);
    Assert.NotNull(response.Metadata);
    Assert.Equal(10, response.Metadata["prompt_tokens"]);
    Assert.Equal(5, response.Metadata["completion_tokens"]);
    Assert.Equal(15, response.Metadata["total_tokens"]);

    // Verify the request was sent correctly
    var sentRequest = handler.LastRequest;
    Assert.NotNull(sentRequest);
    Assert.Contains("Bearer test-api-key", sentRequest.Headers.GetValues("Authorization").First());
  }

  [Fact]
  public async Task Provider_Should_Handle_Missing_Usage_Data()
  {
    // Arrange
    var fakeResponse = new
    {
      id = "test-123",
      choices = new[]
      {
        new
        {
          message = new { role = "assistant", content = "Response text" },
          finish_reason = "stop"
        }
      }
      // No usage field
    };

    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(fakeResponse));
    var services = new ServiceCollection();

    services.AddOpenRouterProvider(options =>
    {
      options.ApiKey = "test-api-key";
    });

    services.AddHttpClient<OpenRouterClient>()
      .ConfigurePrimaryHttpMessageHandler(() => handler);

    var serviceProvider = services.BuildServiceProvider();
    var client = serviceProvider.GetRequiredService<OpenRouterClient>();
    var provider = new OpenRouterProvider(client);

    var request = new LlmRequest
    {
      Prompt = "Test",
      Model = "openai/gpt-4"
    };

    // Act
    var response = await provider.GenerateAsync(request);

    // Assert
    Assert.Equal("Response text", response.Content);
    Assert.NotNull(response.Metadata);
    Assert.Empty(response.Metadata); // No usage data was provided
  }

  [Fact]
  public async Task Provider_Should_Send_Optional_Headers()
  {
    // Arrange
    var fakeResponse = new
    {
      id = "test-123",
      choices = new[]
      {
        new
        {
          message = new { role = "assistant", content = "Test" }
        }
      }
    };

    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(fakeResponse));
    var services = new ServiceCollection();

    services.AddOpenRouterProvider(options =>
    {
      options.ApiKey = "test-api-key";
      options.AppName = "TestApp";
      options.Referer = "https://test.com";
    });

    services.AddHttpClient<OpenRouterClient>()
      .ConfigurePrimaryHttpMessageHandler(() => handler);

    var serviceProvider = services.BuildServiceProvider();
    var client = serviceProvider.GetRequiredService<OpenRouterClient>();
    var provider = new OpenRouterProvider(client);

    var request = new LlmRequest
    {
      Prompt = "Test",
      Model = "openai/gpt-4"
    };

    // Act
    await provider.GenerateAsync(request);

    // Assert
    var sentRequest = handler.LastRequest;
    Assert.NotNull(sentRequest);
    Assert.Contains("TestApp", sentRequest.Headers.GetValues("X-Title").First());
    Assert.Contains("https://test.com", sentRequest.Headers.GetValues("HTTP-Referer").First());
  }

  [Fact]
  public async Task Provider_Should_Throw_On_Http_Error()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "Unauthorized");
    var services = new ServiceCollection();

    services.AddOpenRouterProvider(options =>
    {
      options.ApiKey = "bad-key";
    });

    services.AddHttpClient<OpenRouterClient>()
      .ConfigurePrimaryHttpMessageHandler(() => handler);

    var serviceProvider = services.BuildServiceProvider();
    var client = serviceProvider.GetRequiredService<OpenRouterClient>();
    var provider = new OpenRouterProvider(client);

    var request = new LlmRequest
    {
      Prompt = "Test",
      Model = "openai/gpt-4"
    };

    // Act & Assert
    await Assert.ThrowsAsync<HttpRequestException>(
      async () => await provider.GenerateAsync(request));
  }

  [Fact]
  public void ServiceCollection_Extension_Should_Register_Provider()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    services.AddOpenRouterProvider(options =>
    {
      options.ApiKey = "test-key";
    });

    var serviceProvider = services.BuildServiceProvider();

    // Assert
    var provider = serviceProvider.GetService<ILlmProvider>();
    Assert.NotNull(provider);
    Assert.IsType<OpenRouterProvider>(provider);
  }

  [Fact]
  public void ServiceCollection_Extension_Should_Configure_HttpClient()
  {
    // Arrange
    var services = new ServiceCollection();

    // Act
    services.AddOpenRouterProvider(options =>
    {
      options.ApiKey = "test-key";
    });

    var serviceProvider = services.BuildServiceProvider();

    // Assert
    var client = serviceProvider.GetRequiredService<OpenRouterClient>();
    Assert.NotNull(client);
  }
}

/// <summary>
/// Fake HTTP message handler for testing.
/// </summary>
internal class FakeHttpMessageHandler : HttpMessageHandler
{
  private readonly HttpStatusCode _statusCode;
  private readonly string _responseContent;

  public HttpRequestMessage? LastRequest { get; private set; }

  public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
  {
    _statusCode = statusCode;
    _responseContent = responseContent;
  }

  protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    LastRequest = request;

    var response = new HttpResponseMessage(_statusCode)
    {
      Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
    };

    return Task.FromResult(response);
  }
}
