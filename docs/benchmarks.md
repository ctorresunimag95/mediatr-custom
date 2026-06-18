# Benchmark — Custom Dispatcher vs MediatR

## Purpose

This benchmark validates that replacing MediatR with the in-house [custom dispatcher](custom-dispatcher.md) does **not** introduce a performance regression for the core operation teams rely on: dispatching a request and executing its handler. It backs the decision recorded in [ADR-001](ADR-001-mediatr-alternatives.md) with measured numbers rather than assumption.

MediatR is referenced at version **12.5.0** — the last release published under the free, open-source Apache-2.0 license, before the move to commercial licensing that motivated the migration.

---

## Methodology

- **Tool:** [BenchmarkDotNet](https://benchmarkdotnet.org/), project at [`Benchmarks/`](../Benchmarks).
- **Runtime:** .NET 10.0, Release build.
- **MediatR:** 12.5.0 (free / Apache-2.0).
- **Diagnostics:** `[MemoryDiagnoser]` for allocation tracking.
- **Workload:** a trivial request returning a `Pong(int)` record. The handler does no real work, so each measurement reflects **dispatch + pipeline overhead only**, not business logic.
- **Like-for-like contracts:** the custom side uses the dispatcher's own `IRequest<Pong>` (not `ICommand`/`IQuery`) so the request shape mirrors MediatR's `IRequest<Pong>` as closely as possible.

Four scenarios are measured:

| Scenario | Custom dispatcher | MediatR |
|---|---|---|
| **Pure** | decorators disabled (`UseLogging = false`, `UseValidation = false`) | no `IPipelineBehavior` registered |
| **Decorated** | built-in `LoggingDecorator` + `ValidatorDecorator` | equivalent `LoggingBehavior` + `ValidationBehavior` (`IPipelineBehavior`) mirroring the decorators |

The "decorated" pipelines are intentionally equivalent: both log start/finish/error and run the same FluentValidation validator, ordered logging-outermost.

---

## Results

> Indicative run using BenchmarkDotNet's **short job**. Absolute numbers vary by machine; the relative comparison is the takeaway.

| Method                     | Mean        | Error     | StdDev     | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |------------:|----------:|-----------:|------:|-------:|----------:|------------:|
| CustomDispatcher_Pure      |    87.88 ns |  4.003 ns |  11.804 ns |  1.02 | 0.0134 |     168 B |        1.00 |
| MediatR_Pure               |    82.08 ns |  3.131 ns |   9.084 ns |  0.95 | 0.0178 |     224 B |        1.33 |
| CustomDispatcher_Decorated |   720.96 ns | 33.358 ns |  97.308 ns |  8.35 | 0.1354 |    1704 B |       10.14 |
| MediatR_Decorated          | 1,174.32 ns | 54.289 ns | 160.074 ns | 13.60 | 0.1698 |    2144 B |       12.76 |

`Ratio` and `Alloc Ratio` are relative to the `CustomDispatcher_Pure` baseline.

---

## Conclusions

### Pure dispatch — statistically a tie, leaner on memory
- **88 ns vs 82 ns** is within measurement error (StdDev ±9–12 ns). Speed is **equivalent** for raw dispatch.
- The repeatable difference is allocations: the custom dispatcher allocates **168 B vs MediatR's 224 B — about 25% less**. Its cached-wrapper + `ConcurrentDictionary` resolution path is leaner than MediatR's per-send machinery.

### Decorated — the custom pipeline is faster and lighter
- **721 ns vs 1174 ns (~1.6× faster)** and **1704 B vs 2144 B**. With an equivalent logging + validation pipeline, the custom decorators come out ahead on both time and memory.

### Perspective — overhead is negligible in practice
- All numbers are in **nanoseconds**. Any real handler touching a database, HTTP service, or disk operates in microseconds-to-milliseconds, so dispatch overhead is irrelevant to end-to-end latency for **both** libraries.
- Note the jump from ~88 ns (pure) to ~720 ns (decorated): roughly 90% of the decorated cost is the logging scope and FluentValidation `Task.WhenAll`, **not** dispatch itself. This cost is paid identically whichever library hosts the pipeline.

**Bottom line:** the custom dispatcher is on par with MediatR on raw speed, leaner on allocations, and faster once a pipeline is involved — confirming the migration carries no performance penalty.

---

## Caveats

- **Short-job variance.** These figures come from BenchmarkDotNet's short job; StdDev is ~13% of the mean. For numbers you intend to quote precisely, re-run with the default job (drop `--job short`) for tighter confidence intervals.
- **Reflection-based dispatch.** The custom dispatcher routes through runtime-generated wrapper types (see [ADR-001 Implementation Notes](ADR-001-mediatr-alternatives.md#implementation-notes)). The pure-dispatch result shows this is competitive with MediatR's approach.
- **Synthetic handler.** The handler is a no-op by design, to isolate framework overhead. It is not representative of real per-request cost.