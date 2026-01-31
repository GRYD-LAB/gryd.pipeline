/*
Context:

This project implements a generic, provider-agnostic execution pipeline
designed to orchestrate LLM calls, data transformations, external integrations,
and control-flow decisions.

The pipeline is linear by design. It does not support structural branching
(if/else DAGs). Instead, it relies on:

- Explicit context enrichment
- Conditional execution (skip)
- Short-circuiting (stop execution)
- Composition of pipelines

LLMs are treated as just one type of step.
The engine itself is domain-agnostic and does not infer meaning from data.

All domain meaning is introduced explicitly by pipeline authors through steps.
*/

/*
Architectural Principles:

1. Explicit over implicit:
   - No automatic propagation of data.
   - No hidden dependencies between steps.

2. PipelineContext is a shared blackboard:
   - Steps explicitly read from it.
   - Steps explicitly write to it.

3. The engine does not understand business meaning.
   - Only steps understand domain semantics.

4. LLM providers are dumb transport adapters.
   - No retries, no parsing, no schema validation.

5. Testability is a first-class concern.
   - Every step can be tested in isolation.
   - The full pipeline can be tested with fake providers.
*/

namespace Gryd.Pipeline;

/// <summary>
/// Shared execution context for the pipeline.
/// Acts as an explicit blackboard where steps read and write data.
///
/// IMPORTANT:
/// - Data is enriched explicitly by steps.
/// - The engine never infers, spreads, or transforms data automatically.
/// - Keys and values placed here define the semantic contract between steps.
/// </summary>
public sealed class ExecutionPipelineContext
{
  /// <summary>
  /// Arbitrary data produced and consumed by pipeline steps.
  /// This dictionary is explicitly enriched by steps.
  /// </summary>
  public IDictionary<string, object> Data { get; }
    = new Dictionary<string, object>();

  /// <summary>
  /// Ordered list of step executions, used for observability,
  /// debugging, auditing, and testing.
  /// </summary>
  public IList<StepExecution> Executions { get; }
    = new List<StepExecution>();

  /// <summary>
  /// Stores a value in the execution context.
  /// Overwrites existing values with the same key.
  /// </summary>
  public void Set<T>(string key, T value)
  {
    Data[key] = value!;
  }

  /// <summary>
  /// Retrieves a value from the execution context.
  /// Throws if the key is missing or the type is incompatible.
  /// </summary>
  public T Get<T>(string key)
  {
    return (T)Data[key];
  }

  /// <summary>
  /// Tries to retrieve a value from the execution context.
  /// Returns true if the key exists and the value can be cast to T.
  /// </summary>
  public bool TryGet<T>(string key, out T value)
  {
    if (Data.TryGetValue(key, out var obj) && obj is T typedValue)
    {
      value = typedValue;
      return true;
    }

    value = default!;
    return false;
  }

  /// <summary>
  /// Checks whether a given key exists in the context.
  /// </summary>
  public bool ContainsKey(string key) => Data.ContainsKey(key);

  /// <summary>
  /// Checks whether a given key exists in the context.
  /// </summary>
  public bool Has(string key) => Data.ContainsKey(key);
}
