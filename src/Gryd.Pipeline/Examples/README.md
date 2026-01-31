# Gryd.Pipeline Examples

This directory contains compilable C# examples that demonstrate how to use the Gryd.Pipeline framework.

**Why C# instead of Markdown?**

These examples are actual C# files that compile with the project. This ensures they stay up-to-date with API changes -
if the API changes, the examples will fail to compile, alerting us to update them.

## Example Files

### BasicPipelineExamples.cs

- Simple transformation pipeline
- Conditional stop execution
- Observability and debugging
- Pipeline composition

### ExternalCallExamples.cs

- External API integration
- Chained external calls
- Complex input mapping

### LlmStepExamples.cs

> **Note**: All examples show concrete `LlmStep<T>` implementations. `LlmStep<T>` is abstract and cannot be instantiated directly.

- Basic LLM step with prompt templating (concrete subclass pattern)
- Multi-step LLM pipeline
- Custom output parsing
- RAG-style pipeline with document retrieval

### CustomStepExamples.cs

- Custom validation step
- Retry step wrapper
- Logging step
- Conditional execution step

## Running Examples

These examples are meant to be referenced and copied into your own projects. They demonstrate patterns and best
practices but are not executable programs themselves (no `Main` method).

To use them:

1. Browse the example files to find the pattern you need
2. Copy the relevant code into your project
3. Adapt it to your specific use case

## Key Principles Demonstrated

1. **Explicit Data Flow**: Data must be explicitly read from and written to the context
2. **No Hidden Dependencies**: Each step declares what it needs via input mappers
3. **Testability First**: Steps can be tested in isolation with fake providers
4. **Observable Execution**: Every step execution is recorded for debugging
5. **Linear Flow**: No branching - use composition and stop conditions instead

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](../../../LICENSE) file for details.

