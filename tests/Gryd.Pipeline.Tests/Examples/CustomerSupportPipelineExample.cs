namespace Gryd.Pipeline.Tests.Examples;

using Steps;
using Fakes;
using Microsoft.Extensions.Options;
using System.Text.Json;

/// <summary>
/// Complete example: Customer Support Assistant Pipeline
/// This example shows a realistic pipeline that:
/// 1. Validates user input
/// 2. Fetches customer data from external system
/// 3. Uses LLM to generate a personalized response
/// 4. Logs the interaction
/// </summary>
public class CustomerSupportPipelineExample
{
  [Fact]
  public async Task Complete_Customer_Support_Pipeline_Example()
  {
    // ============================================================
    // STEP 1: Validate Input
    // ============================================================
    var validateStep = new SimpleTransformStep(
      "ValidateInput",
      ctx =>
      {
        var query = ctx.Get<string>("customer_query");

        if (string.IsNullOrWhiteSpace(query))
        {
          ctx.Set("validation_error", "Query cannot be empty");
          return;
        }

        ctx.Set("validation_passed", true);
      });

    // ============================================================
    // STEP 2: Fetch Customer Data (External Call)
    // ============================================================
    var fetchCustomerStep = new ExternalCallStep<CustomerData>(
      "FetchCustomerData",
      call: async ctx =>
      {
        var customerId = ctx.Get<string>("customer_id");

        // Simulate external API call
        await Task.Delay(10);

        return new CustomerData
        {
          Id = customerId,
          Name = "John Doe",
          Tier = "Premium",
          PurchaseHistory = new[] { "Widget A", "Widget B" }
        };
      },
      saveResult: (ctx, data) =>
      {
        ctx.Set("customer", data);
        ctx.Set("customer_name", data.Name);
        ctx.Set("customer_tier", data.Tier);
      });

    // ============================================================
    // STEP 3: Generate LLM Response
    // ============================================================
    var provider = new FakeLlmProvider(prompt =>
    {
      // Simulate LLM understanding the context
      if (prompt.Contains("Premium"))
        return "Thank you for being a valued Premium customer! We appreciate your loyalty.";
      else
        return "Thank you for your inquiry. How may we assist you today?";
    });

    var llmStep = new CustomerResponseStep(
      provider,
      Options.Create<LlmStepOptions>(new CustomerLlmStepOptions { Model = "gpt-4", Temperature = 0.7 }),
      new JsonSerializerOptions());

    // ============================================================
    // STEP 4: Log Interaction
    // ============================================================
    var logStep = new SimpleTransformStep(
      "LogInteraction",
      ctx =>
      {
        var log = new InteractionLog
        {
          CustomerId = ctx.Get<string>("customer_id"),
          Query = ctx.Get<string>("customer_query"),
          Response = ctx.Get<string>("generated_response"),
          Timestamp = DateTime.UtcNow
        };

        ctx.Set("interaction_logged", true);
        ctx.Set("log", log);
      });

    // ============================================================
    // BUILD AND EXECUTE PIPELINE
    // ============================================================
    var runner = new PipelineRunner();

    // Setup: Add initial input step
    var setupStep = new SimpleTransformStep("SetupInput", ctx =>
    {
      ctx.Set("customer_id", "CUST-123");
      ctx.Set("customer_query", "How do I return a product?");
    });

    // Build complete pipeline with setup step
    var pipeline = new PipelineBuilder()
      .With(setupStep)
      .With(validateStep)
      .With(fetchCustomerStep)
      .With(llmStep)
      .With(logStep)
      .Build();

    // Execute
    var context = await runner.RunAsync(pipeline, CancellationToken.None);

    // ============================================================
    // VERIFY RESULTS
    // ============================================================

    // All steps executed successfully (5 steps including setup)
    Assert.Equal(5, context.Executions.Count);
    Assert.All(context.Executions, e => Assert.True(e.Success));

    // Validation passed
    Assert.True(context.Get<bool>("validation_passed"));

    // Customer data fetched
    var customer = context.Get<CustomerData>("customer");
    Assert.Equal("John Doe", customer.Name);
    Assert.Equal("Premium", customer.Tier);

    // LLM response generated
    var response = context.Get<string>("generated_response");
    Assert.Contains("Premium customer", response);

    // Interaction logged
    Assert.True(context.Get<bool>("interaction_logged"));
    var log = context.Get<InteractionLog>("log");
    Assert.Equal("CUST-123", log.CustomerId);

    // ============================================================
    // OBSERVABILITY: Inspect execution timeline
    // ============================================================
    foreach (var execution in context.Executions)
    {
      var duration = execution.FinishedAt - execution.StartedAt;
      System.Diagnostics.Debug.WriteLine(
        $"Step: {execution.StepName}, Duration: {duration.TotalMilliseconds}ms");
    }
  }

  // Supporting types for the example
  public class CustomerData
  {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Tier { get; init; }
    public required string[] PurchaseHistory { get; init; }
  }

  public class InteractionLog
  {
    public required string CustomerId { get; init; }
    public required string Query { get; init; }
    public required string Response { get; init; }
    public DateTime Timestamp { get; init; }
  }

  // Concrete LlmStep implementation for customer support
  private class CustomerResponseStep : LlmStep
  {
    public override string Name => "GenerateResponse";

    protected override string PromptTemplate => @"
Customer: {customer_name} ({customer_tier} tier)
Query: {query}

Generate a helpful and personalized response:";

    public CustomerResponseStep(
      Llm.ILlmProvider provider,
      IOptions<LlmStepOptions> options,
      JsonSerializerOptions jsonOptions) : base(provider, options, jsonOptions)
    {
    }

    protected override IDictionary<string, string> MapInputs(ExecutionPipelineContext context)
    {
      return new Dictionary<string, string>
      {
        ["customer_name"] = context.Get<string>("customer_name"),
        ["customer_tier"] = context.Get<string>("customer_tier"),
        ["query"] = context.Get<string>("customer_query")
      };
    }

    protected override void WriteResult(ExecutionPipelineContext context, string rawResult)
    {
      // Child class decides whether to parse the raw result
      context.Set("generated_response", rawResult.Trim());
    }
  }

  private record CustomerLlmStepOptions : LlmStepOptions;
}
