# MediatR library alternatives

| Field | Value |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-06-16 |
| **Author** | Camilo Torres |
| **ADR Number** | |

---

## Context and Problem Statement

Several projects in the organization use the Mediator pattern to decouple request handling from business logic. The de-facto library for this in .NET has been [MediatR](https://github.com/jbogard/MediatR). The pattern centralizes dispatching of commands and queries through a single `IMediator` interface, keeping controllers and endpoints thin and making cross-cutting concerns (logging, validation) easy to apply uniformly.

With MediatR moving to a commercial licensing model, continued use in commercial projects requires purchasing a license per developer or per project. This introduces a recurring cost, a legal obligation, and an external dependency that teams must manage over time.

A decision is needed on how to continue providing the Mediator pattern in affected projects without the licensing constraint.

---

## Current Process (AS-IS)

Projects currently depend directly on the `MediatR` NuGet package and its companion `MediatR.Extensions.Microsoft.DependencyInjection`. Handler registration, request dispatching, and pipeline behavior (e.g. `IPipelineBehavior<,>`) are all provided by this library.

As MediatR transitions to a paid model, any commercial use requires a license agreement. Projects that do not obtain a license face:

- Compliance and legal risk from unlicensed use.
- Dependency on a third-party vendor's pricing and licensing decisions.
- Potential forced migration under time pressure if the license cost is deemed unacceptable.

---

## Decision Drivers

- **License cost** — MediatR now requires a commercial license for most usage; a free alternative is needed.
- **Minimal footprint** — Teams only rely on a small subset of MediatR's surface area (dispatch, handlers, pipeline behaviors). A full-featured replacement is unnecessary.
- **Maintainability** — The chosen solution should be straightforward for any team member to read, understand, and modify.
- **Security** — The team must be able to patch vulnerabilities quickly, without waiting on a third-party release cycle.
- **Continuity** — Familiar concepts (`IRequest`, `IRequestHandler<,>`, a `Send`-style dispatcher) lower the learning curve for developers already experienced with MediatR.
- **Extensibility** — The pipeline must support pluggable decorators for logging, validation, and custom cross-cutting concerns.

---

## Considered Options

### Option 1 — Wolverine

[Wolverine](https://wolverine.netlify.app/) (formerly Jasper) is an open-source .NET messaging and mediator framework by Jeremy Miller. It covers both in-process mediation and distributed messaging (message bus, queues).

| Pros | Cons |
|---|---|
| Open source (MIT) | Large surface area — much more than the team needs |
| Actively maintained | Opinionated conventions may conflict with existing patterns |
| Supports local and distributed messaging | Steeper learning curve |
| Strong community and documentation | Brings in additional infrastructure dependencies |

### Option 2 — Brighter

[Brighter](https://www.goparamore.io/) is an open-source Command Processor / dispatcher library by Ian Cooper, implementing patterns from *Patterns of Enterprise Application Architecture*.

| Pros | Cons |
|---|---|
| Open source (MIT) | Heavier abstraction layer than needed |
| Battle-tested in production | Last major update less frequent than alternatives |
| Supports outbox pattern and retries | Concepts diverge slightly from MediatR, requiring migration effort |
| Pipeline middleware support | Distributed features add complexity not required for in-process use |

### Option 3 — FastEndpoints

[FastEndpoints](https://fast-endpoints.com/) is an open-source ASP.NET Core library that implements the REPR (Request-Endpoint-Response) pattern. Beyond endpoint handling, it ships a built-in in-process command bus (`ICommandHandler<TCommand, TResult>`) and an event bus (`IEventHandler<TEvent>`), making it a viable MediatR replacement for projects that are also willing to adopt its endpoint model.

| Pros | Cons |
|---|---|
| Open source (MIT) | Adopting the command bus also means adopting the endpoint pattern — it is not a standalone dispatcher library |
| Built-in command/event bus with no extra packages | Replaces the entire request-handling layer (controllers, minimal APIs), which is a large migration surface |
| High performance (benchmarked faster than MediatR + minimal APIs) | Teams that want only the mediator pattern carry the full weight of the library |
| Rich built-in pipeline: validation, pre/post processors, middleware | Opinionated conventions may conflict with existing project structure |
| Actively maintained, good documentation | Adds a significant dependency; the team does not own the source |

### Option 4 — Custom dispatcher library *(Preferred)*

A self-contained, minimal dispatcher library built in-house, tailored to the team's actual usage and inspired by MediatR's core concepts. The organization owns the source entirely.

Key capabilities:

- A single `IDispatcher` interface with `SendAsync` overloads for commands (with and without a result), queries, and raw `IRequest<T>` / `IRequest` contracts.
- **Native `IRequest` support** — projects already using MediatR-style `IRequest<T>` / `IRequestHandler<,>` can dispatch without renaming any types; the semantic `ICommand`/`IQuery` interfaces are available but optional.
- Automatic handler registration via assembly scanning — no per-handler DI wiring.
- A pluggable decorator pipeline with built-in `LoggingDecorator` and `ValidatorDecorator` (FluentValidation), plus support for global and targeted custom decorators. Decorators apply to all handlers regardless of which contract (`ICommand`, `IQuery`, or `IRequest`) they use.
- Configured with a single `AddCustomDispatcher<TAssemblyMarker>()` call in `Program.cs`.

| Pros | Cons |
|---|---|
| Zero license cost, owned entirely by the organization | Team is responsible for maintenance and .NET compatibility updates |
| Minimal surface area — only what teams actually use | No third-party ecosystem or community extensions |
| `IRequest`/`IRequestHandler` work as-is — zero rename effort for existing MediatR code | No upstream test suite — team owns all tests |
| Vulnerabilities patched immediately, no upstream dependency | |
| Readable in under an hour; any team member can modify it | |

See [custom-dispatcher.md](custom-dispatcher.md) for the full technical reference.

---

## Decision Outcome

**Chosen option: Custom dispatcher library.**

The custom library replicates only the features teams actually use — a single `IDispatcher` interface, automatic handler registration via assembly scanning, and a pluggable decorator pipeline — without taking on external vendor risk or unused complexity.

### Advantages

- **Self-hosted** — The source code lives in the organization's own repository. No license agreement, no vendor lock-in, no external package to audit for updates.
- **Easy to manage** — The implementation is intentionally small (a handful of files). Any developer on the team can read and understand the entire library in under an hour.
- **Security issue support** — Vulnerabilities can be patched immediately by the team, without waiting on a third-party maintainer's release cycle or disclosure timeline.
- **Zero license cost** — No per-developer or per-project fees, now or in the future.
- **Familiar concepts** — `IRequest<T>`, `IRequest`, and `IRequestHandler<,>` are supported natively; the `ICommand`/`IQuery` semantic interfaces are available but optional. Existing MediatR handlers can be dispatched with zero type renames.
- **Tailored extensibility** — The decorator pipeline (`IDecorator<,>`) is designed around the team's actual cross-cutting concerns (logging, validation) while remaining open for custom decorators.

### Consequences

- **Maintenance ownership** — The team is fully responsible for bug fixes, feature additions, and .NET compatibility updates. This is manageable given the library's small size but is a real ongoing commitment.
- **No community ecosystem** — Third-party extensions or integrations built for MediatR (e.g. community pipeline behaviors) are not directly compatible and would need to be ported.
- **Migration effort** — Existing projects using MediatR need only replace the NuGet package reference and the DI registration call (`AddMediatR` → `AddCustomDispatcher`). `IRequest`, `IRequestHandler`, and validators require no changes. Adopting the `ICommand`/`IQuery` semantic interfaces is optional and can be done incrementally.
- **No notification bus equivalent** — `INotification` / `INotificationHandler<T>` are out of scope for this library. Teams that rely on publish-subscribe patterns must keep MediatR temporarily for notifications or adopt a dedicated event bus.
- **One handler per request type** — The dispatcher assumes each request resolves to exactly one handler. Fan-out, multiple competing handlers, and notification-style dispatch are not supported.
- **Host-oriented defaults** — `IDispatcher` is registered as `Transient`, while handlers default to `Scoped` to align with typical ASP.NET Core request lifetimes. Teams can override handler lifetime when needed, but the lifetime parameter on `AddCustomDispatcher` does not change the dispatcher's own lifetime.
- **Testing responsibility** — Unit and integration tests for the dispatcher itself are owned by the team. There is no upstream test suite to rely on.

### Implementation Notes

- **Validation setup is explicit** — `ValidatorDecorator` runs automatically when enabled, but projects must still register validators with FluentValidation (for example, `AddValidatorsFromAssemblyContaining<Program>()`).
- **Decorator order is registration-driven** — The built-in registration adds `ValidatorDecorator` first and `LoggingDecorator` second. The last registered decorator becomes the outermost wrapper.
- **Runtime dispatch is simple by design** — Requests are routed through runtime-generated wrapper types rather than source-generated dispatch code. This keeps the implementation small and readable, at the cost of some reflection-based dispatch overhead.

---

## System Architecture

The following diagram shows how a request flows through the dispatcher pipeline, from the call site to the handler and back. The same pipeline applies to both `IRequest<TResponse>` and void `IRequest` handlers.

```mermaid
flowchart LR
        A([Command / Query / IRequest]) --> B[IDispatcher.SendAsync]
    B --> C[LoggingDecorator]
    C --> D[ValidatorDecorator]
    D --> E[Custom Decorators\noptional]
    E --> F[IRequestHandler\nYour Handler]
        F --> G([Response or completion])

    style A fill:#4A90D9,color:#fff,stroke:none
    style G fill:#4A90D9,color:#fff,stroke:none
    style B fill:#7B68EE,color:#fff,stroke:none
    style C fill:#5BA85A,color:#fff,stroke:none
    style D fill:#5BA85A,color:#fff,stroke:none
    style E fill:#E8A838,color:#fff,stroke:none
    style F fill:#C0392B,color:#fff,stroke:none
```

**Pipeline order (outermost → innermost):**

```
Command / Query / IRequest
    └─▶ LoggingDecorator        (logs start, finish, and errors)
            └─▶ ValidatorDecorator   (runs FluentValidation before the handler)
                    └─▶ Custom Decorators    (metrics, caching, auth checks, etc.)
                            └─▶ Handler      (your business logic)
                                                                                                                                                └─▶ Response or completion
```

Built-in registration order:

```csharp
services.Decorate(typeof(IRequestHandler<,>), typeof(ValidatorDecorator<,>));
services.Decorate(typeof(IRequestHandler<,>), typeof(LoggingDecorator<,>));
```

Resulting call stack:

```text
LoggingDecorator.Handle()
        -> ValidatorDecorator.Handle()
                -> YourHandler.Handle()
```

The last registered decorator becomes the outer wrapper, which is why logging runs around validation and the handler.

---

## More Information

- [Custom Dispatcher — Full Technical Reference](custom-dispatcher.md) — complete API surface, configuration options, decorator pipeline details, and usage examples.
- [Migration Guide — MediatR to Custom Dispatcher](migration-guide.md) — step-by-step instructions with before/after code snippets for migrating an existing MediatR project.
- [Benchmark — Custom Dispatcher vs MediatR](benchmarks.md) — BenchmarkDotNet comparison of dispatch and pipeline overhead against MediatR 12.5.0, confirming the migration carries no performance penalty.
