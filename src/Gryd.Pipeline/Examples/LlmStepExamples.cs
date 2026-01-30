namespace Gryd.Pipeline.Examples;

using Llm;
using Steps;

/// <summary>
/// Examples demonstrating LlmStep usage with different providers.
/// </summary>
public static class LlmStepExamples
{
    /// <summary>
    /// Basic LLM step example with prompt templating.
    /// </summary>
    public static async Task BasicLlmStepExample()
    {
        var provider = new FakeLlmProvider(prompt => $"Response to: {prompt}");

        var llmStep = new LlmStep<string>(
            name: "GenerateResponse",
            provider: provider,
            inputMapper: ctx => new Dictionary<string, string>
            {
                ["query"] = ctx.Get<string>("user_query")
            },
            promptTemplate: "Answer this question: {query}",
            outputParser: response => response.Trim(),
            outputKey: "llm_response",
            model: "gpt-4",
            temperature: 0.7);

        var pipeline = new PipelineBuilder()
            .With(llmStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new PipelineExecutionContext();
        context.Set("user_query", "What is a pipeline?");

        await runner.RunAsync(pipeline, context);

        var response = context.Get<string>("llm_response");
        Console.WriteLine(response);
    }

    /// <summary>
    /// Multi-step LLM pipeline with data enrichment.
    /// </summary>
    public static async Task MultiStepLlmPipeline()
    {
        var provider = new FakeLlmProvider(prompt =>
            prompt.Contains("classify") ? "technical_question" : "Here is the answer");

        var prepareStep = new TransformStep("PrepareInput", ctx =>
        {
            var rawQuery = ctx.Get<string>("raw_query");
            ctx.Set("cleaned_query", rawQuery.Trim());
            return Task.CompletedTask;
        });

        var classifyStep = new LlmStep<string>(
            name: "ClassifyIntent",
            provider: provider,
            inputMapper: ctx => new Dictionary<string, string>
            {
                ["query"] = ctx.Get<string>("cleaned_query")
            },
            promptTemplate: "Classify the intent of: {query}",
            outputParser: response => response.Trim().ToLower(),
            outputKey: "intent",
            model: "gpt-3.5-turbo",
            temperature: 0.3);

        var respondStep = new LlmStep<string>(
            name: "GenerateResponse",
            provider: provider,
            inputMapper: ctx => new Dictionary<string, string>
            {
                ["intent"] = ctx.Get<string>("intent"),
                ["query"] = ctx.Get<string>("cleaned_query")
            },
            promptTemplate: "Intent: {intent}\nQuery: {query}\n\nProvide a response:",
            outputParser: response => response.Trim(),
            outputKey: "final_response",
            model: "gpt-4",
            temperature: 0.7);

        var pipeline = new PipelineBuilder()
            .With(prepareStep)
            .With(classifyStep)
            .With(respondStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new PipelineExecutionContext();
        context.Set("raw_query", "  How do I configure the pipeline?  ");

        await runner.RunAsync(pipeline, context);

        var intent = context.Get<string>("intent");
        var response = context.Get<string>("final_response");

        Console.WriteLine($"Intent: {intent}");
        Console.WriteLine($"Response: {response}");
    }

    /// <summary>
    /// LLM step with custom output parsing.
    /// </summary>
    public static async Task CustomOutputParsingExample()
    {
        var provider = new FakeLlmProvider(_ => "Answer: 42\nExplanation: This is the answer");

        var llmStep = new LlmStep<ParsedResponse>(
            name: "ExtractStructuredData",
            provider: provider,
            inputMapper: ctx => new Dictionary<string, string>
            {
                ["question"] = ctx.Get<string>("question")
            },
            promptTemplate: "Question: {question}",
            outputParser: raw =>
            {
                var lines = raw.Split('\n');
                var answer = lines.FirstOrDefault(l => l.StartsWith("Answer:"))?.Replace("Answer:", "").Trim();
                var explanation = lines.FirstOrDefault(l => l.StartsWith("Explanation:"))?.Replace("Explanation:", "").Trim();

                return new ParsedResponse
                {
                    Answer = answer ?? string.Empty,
                    Explanation = explanation ?? string.Empty
                };
            },
            outputKey: "parsed_response");

        var pipeline = new PipelineBuilder()
            .With(llmStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new PipelineExecutionContext();
        context.Set("question", "What is the meaning of life?");

        await runner.RunAsync(pipeline, context);

        var parsed = context.Get<ParsedResponse>("parsed_response");
        Console.WriteLine($"Answer: {parsed.Answer}");
        Console.WriteLine($"Explanation: {parsed.Explanation}");
    }

    /// <summary>
    /// RAG-like pipeline with document retrieval and LLM generation.
    /// </summary>
    public static async Task RagStylePipeline()
    {
        var provider = new FakeLlmProvider(prompt =>
            $"Based on the documents, here's the answer to your question.");

        var retrieveDocsStep = new ExternalCallStep<List<string>>(
            name: "RetrieveDocuments",
            call: async ctx =>
            {
                // Simulate document retrieval
                await Task.Delay(10);
                return new List<string>
                {
                    "Document 1: Pipeline basics",
                    "Document 2: Advanced usage",
                    "Document 3: Best practices"
                };
            },
            saveResult: (ctx, docs) => ctx.Set("retrieved_docs", docs));

        var formatContextStep = new TransformStep("FormatContext", ctx =>
        {
            var docs = ctx.Get<List<string>>("retrieved_docs");
            var context = string.Join("\n", docs);
            ctx.Set("document_context", context);
            return Task.CompletedTask;
        });

        var generateStep = new LlmStep<string>(
            name: "GenerateAnswer",
            provider: provider,
            inputMapper: ctx => new Dictionary<string, string>
            {
                ["query"] = ctx.Get<string>("query"),
                ["context"] = ctx.Get<string>("document_context")
            },
            promptTemplate: @"Context:
{context}

Question: {query}

Answer based on the context:",
            outputParser: response => response.Trim(),
            outputKey: "answer");

        var pipeline = new PipelineBuilder()
            .With(retrieveDocsStep)
            .With(formatContextStep)
            .With(generateStep)
            .Build();

        var runner = new PipelineRunner();
        var context = new PipelineExecutionContext();
        context.Set("query", "How do I use pipelines?");

        await runner.RunAsync(pipeline, context);

        var answer = context.Get<string>("answer");
        Console.WriteLine(answer);
    }

    // Supporting types
    public class ParsedResponse
    {
        public string Answer { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }

    // Fake LLM provider for examples
    private class FakeLlmProvider : ILlmProvider
    {
        private readonly Func<string, string> _responseFunc;

        public FakeLlmProvider(Func<string, string> responseFunc)
        {
            _responseFunc = responseFunc;
        }

        public Task<LlmRawResponse> GenerateAsync(
            LlmRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new LlmRawResponse
            {
                Content = _responseFunc(request.Prompt),
                Metadata = new Dictionary<string, object>
                {
                    ["prompt_tokens"] = 10,
                    ["completion_tokens"] = 20,
                    ["total_tokens"] = 30
                }
            });
        }
    }
}
