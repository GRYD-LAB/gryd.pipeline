namespace Gryd.Pipeline.Examples;

using Steps;

/// <summary>
/// Examples demonstrating ExternalCallStep usage.
/// </summary>
public static class ExternalCallExamples
{
    // Example data models
    public class WeatherData
    {
        public string City { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public string Condition { get; set; } = string.Empty;
    }

    public class ApiRequest
    {
        public string Query { get; set; } = string.Empty;
    }

    public class ApiResponse
    {
        public string Result { get; set; } = string.Empty;
        public int StatusCode { get; set; }
    }

    /// <summary>
    /// Example of calling an external API with ExternalCallStep.
    /// </summary>
    public static async Task ExternalApiCallExample()
    {
        // Simulated external API client
        var weatherApi = new FakeWeatherApi();

        var externalStep = new ExternalCallStep<WeatherData>(
            name: "FetchWeather",
            call: async ctx =>
            {
                var city = ctx.Get<string>("city");
                return await weatherApi.GetWeatherAsync(city, CancellationToken.None);
            },
            saveResult: (ctx, weather) =>
            {
                ctx.Set("weather", weather);
                ctx.Set("temperature", weather.Temperature);
                ctx.Set("condition", weather.Condition);
            });

        var pipeline = new PipelineBuilder()
            .With(externalStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new PipelineExecutionContext();
        context.Set("city", "San Francisco");

        await runner.RunAsync(pipeline, context);

        var weather = context.Get<WeatherData>("weather");
        Console.WriteLine($"Temperature: {weather.Temperature}°C");
    }

    /// <summary>
    /// Example of chaining multiple external calls.
    /// </summary>
    public static async Task ChainedExternalCallsExample()
    {
        var api = new FakeExternalApi();

        var lookupStep = new ExternalCallStep<string>(
            name: "LookupUserId",
            call: async ctx =>
            {
                var username = ctx.Get<string>("username");
                return await api.GetUserIdAsync(username, CancellationToken.None);
            },
            saveResult: (ctx, userId) => ctx.Set("user_id", userId));

        var fetchDataStep = new ExternalCallStep<string>(
            name: "FetchUserData",
            call: async ctx =>
            {
                var userId = ctx.Get<string>("user_id");
                return await api.GetUserDataAsync(userId, CancellationToken.None);
            },
            saveResult: (ctx, data) => ctx.Set("user_data", data));

        var pipeline = new PipelineBuilder()
            .With(lookupStep)
            .With(fetchDataStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new PipelineExecutionContext();
        context.Set("username", "john_doe");

        await runner.RunAsync(pipeline, context);

        Console.WriteLine(context.Get<string>("user_data"));
    }

    /// <summary>
    /// Example with complex input mapping.
    /// </summary>
    public static async Task ComplexInputMappingExample()
    {
        var api = new FakeExternalApi();

        var enrichmentStep = new ExternalCallStep<ApiResponse>(
            name: "EnrichData",
            call: async ctx =>
            {
                var request = new ApiRequest
                {
                    Query = $"{ctx.Get<string>("name")}:{ctx.Get<string>("category")}"
                };
                return await api.ProcessRequestAsync(request, CancellationToken.None);
            },
            saveResult: (ctx, response) =>
            {
                ctx.Set("api_result", response.Result);
                ctx.Set("status_code", response.StatusCode);
            });

        var pipeline = new PipelineBuilder()
            .With(enrichmentStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new PipelineExecutionContext();
        context.Set("name", "Product");
        context.Set("category", "Electronics");

        await runner.RunAsync(pipeline, context);
    }

    // Fake implementations for examples
    private class FakeWeatherApi
    {
        public Task<WeatherData> GetWeatherAsync(string city, CancellationToken ct)
        {
            return Task.FromResult(new WeatherData
            {
                City = city,
                Temperature = 22.5,
                Condition = "Sunny"
            });
        }
    }

    private class FakeExternalApi
    {
        public Task<string> GetUserIdAsync(string username, CancellationToken ct)
        {
            return Task.FromResult($"user_{username}");
        }

        public Task<string> GetUserDataAsync(string userId, CancellationToken ct)
        {
            return Task.FromResult($"Data for {userId}");
        }

        public Task<ApiResponse> ProcessRequestAsync(ApiRequest request, CancellationToken ct)
        {
            return Task.FromResult(new ApiResponse
            {
                Result = $"Processed: {request.Query}",
                StatusCode = 200
            });
        }
    }
}
