# Custom Dispatcher — Technical Reference

A small, self-contained mediator / dispatcher for .NET. It keeps the parts most teams actually rely on: a single entry-point interface, automatic handler registration, and a pluggable decorator pipeline.

## Repository structure

```
mediatr-custom/
├── Dispatcher/        # Class library (net10.0) — the reusable dispatcher
└── ToDoApi/           # ASP.NET Core 10 reference implementation
```

## Installation

Reference the `Dispatcher` project directly, or publish it as a NuGet package:

```xml
<ItemGroup>
  <ProjectReference Include="../Dispatcher/Dispatcher.csproj" />
</ItemGroup>
```

### Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Scrutor` | 7.x | Assembly scanning and `.Decorate()` support |
| `FluentValidation` | 12.x | Used by the built-in `ValidatorDecorator` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.x | `IServiceCollection`, `IServiceProvider` |
| `Microsoft.Extensions.Logging.Abstractions` | 10.x | `ILogger<T>` used by `LoggingDecorator` |
| .NET | 10 | Target framework |

---

## Core concepts

| Concept | Interface | Purpose |
|---|---|---|
| Dispatcher | `IDispatcher` | Single entry point injected at call sites; routes a request to its handler via `SendAsync` |
| Command (with result) | `ICommand<TResponse>` | A request that changes state and returns a result |
| Command (no result) | `ICommand` | A request that changes state and returns nothing |
| Query | `IQuery<TResponse>` | A read-only request that returns a result |
| Handler | `ICommandHandler<TRequest, TResponse>` / `ICommandHandler<TRequest>` / `IQueryHandler<TRequest, TResponse>` | Handles exactly one request type |
| Decorator | `IDecorator<TRequest, TResponse>` / `IDecorator<TRequest>` | Wraps a handler to add cross-cutting behavior |

`ICommand`, `ICommand<T>`, and `IQuery<T>` all extend the generic `IRequest<TResponse>` contract. Handlers implement `IRequestHandler<TRequest, TResponse>`, which is what the dispatcher and the assembly scanner key off of.

### IDispatcher interface

Depend on `IDispatcher` (never the concrete type). The correct overload is resolved by the shape of the request:

```csharp
public interface IDispatcher
{
    // Semantic wrappers — preferred for new code
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);
    Task             SendAsync(ICommand command, CancellationToken cancellationToken);
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse>   query,   CancellationToken cancellationToken);

    // Base contracts — for projects using IRequest<T> / IRequest directly
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);
    Task             SendAsync(IRequest request, CancellationToken cancellationToken);
}
```

The `IRequest`-based overloads exist for interoperability — any class that implements `IRequest<T>` or `IRequest` (including `ICommand<T>`, `ICommand`, and `IQuery<T>`) can be dispatched through them. The semantic overloads (`ICommand`, `IQuery`) are more specific and will always be preferred by C# overload resolution, so existing call sites are unaffected.

---

## Defining requests and handlers

### Raw request (base contracts)

Projects migrating from MediatR or preferring to stay on the base contracts can implement `IRequest<T>` / `IRequest` directly and dispatch without renaming to `ICommand`/`IQuery`:

```csharp
// Typed request — dispatched via SendAsync(IRequest<T>)
public sealed record SearchToDosRequest(string Term) : IRequest<IEnumerable<ToDo>>;

internal sealed class SearchToDosRequestHandler : IRequestHandler<SearchToDosRequest, IEnumerable<ToDo>>
{
    private readonly ToDoRepository _repository;
    public SearchToDosRequestHandler(ToDoRepository repository) => _repository = repository;

    public Task<IEnumerable<ToDo>> Handle(SearchToDosRequest request, CancellationToken cancellationToken)
        => Task.FromResult(_repository.GetAll()
            .Where(t => t.Description.Contains(request.Term, StringComparison.OrdinalIgnoreCase)));
}

// Void request — dispatched via SendAsync(IRequest)
public sealed record DeleteToDoRequest(Guid Id) : IRequest;

internal sealed class DeleteToDoRequestHandler : IRequestHandler<DeleteToDoRequest>
{
    private readonly ToDoRepository _repository;
    public DeleteToDoRequestHandler(ToDoRepository repository) => _repository = repository;

    public Task Handle(DeleteToDoRequest request, CancellationToken cancellationToken)
    {
        _repository.Remove(request.Id);
        return Task.CompletedTask;
    }
}
```

Handlers registered this way are **automatically discovered** by the assembly scanner and **wrapped by all configured decorators** (logging, validation) exactly like command and query handlers.

### Command with a result

```csharp
// 1. Define the request and response
public sealed record CreateToDoCommand(string Description) : ICommand<CreateToDoResponse>;
public sealed record CreateToDoResponse(Guid Id, string Description, bool IsCompleted);

// 2. Define the handler — discovered and registered automatically
internal sealed class CreateToDoCommandHandler : ICommandHandler<CreateToDoCommand, CreateToDoResponse>
{
    private readonly ToDoRepository _repository;

    public CreateToDoCommandHandler(ToDoRepository repository) => _repository = repository;

    public Task<CreateToDoResponse> Handle(CreateToDoCommand command, CancellationToken cancellationToken)
    {
        var todo = new ToDo(command.Description);
        _repository.Add(todo);
        return Task.FromResult(new CreateToDoResponse(todo.Id, todo.Description, todo.IsCompleted));
    }
}
```

### Command with no result

```csharp
public sealed record CompleteTodoCommand(Guid Id) : ICommand;

internal sealed class CompleteTodoCommandHandler : ICommandHandler<CompleteTodoCommand>
{
    private readonly ToDoRepository _repository;

    public CompleteTodoCommandHandler(ToDoRepository repository) => _repository = repository;

    public Task Handle(CompleteTodoCommand command, CancellationToken cancellationToken)
    {
        _repository.Complete(command.Id);
        return Task.CompletedTask;
    }
}
```

### Query

```csharp
public sealed record GetToDosQuery() : IQuery<IEnumerable<ToDo>>;

internal sealed class GetToDosQueryHandler : IQueryHandler<GetToDosQuery, IEnumerable<ToDo>>
{
    private readonly ToDoRepository _repository;

    public GetToDosQueryHandler(ToDoRepository repository) => _repository = repository;

    public Task<IEnumerable<ToDo>> Handle(GetToDosQuery query, CancellationToken cancellationToken)
        => Task.FromResult(_repository.GetAll());
}
```

---

## Dispatching

Inject `IDispatcher` and call `SendAsync`:

```csharp
app.MapPost("/todos", async (
    [FromBody] CreateToDoCommand command,
    [FromServices] IDispatcher dispatcher,
    CancellationToken cancellationToken) =>
{
    var response = await dispatcher.SendAsync(command, cancellationToken);
    return Results.Created($"/todos/{response.Id}", response);
});

// Void command
await dispatcher.SendAsync(new CompleteTodoCommand(id), cancellationToken);

// Query
var todos = await dispatcher.SendAsync(new GetToDosQuery(), cancellationToken);

// Raw IRequest<T> (base contract)
var results = await dispatcher.SendAsync(new SearchToDosRequest(term), cancellationToken);

// Raw IRequest (void, base contract)
await dispatcher.SendAsync(new DeleteToDoRequest(id), cancellationToken);
```

---

## Automatic handler registration

Registration is a single call in `Program.cs`. The generic type parameter tells the scanner which assembly to search:

```csharp
builder.Services.AddCustomDispatcher<Program>();
```

`AddCustomDispatcher<TAssemblyMarker>` uses [Scrutor](https://github.com/khellang/Scrutor) to scan that assembly and register every class that implements `IRequestHandler<,>` or `IRequestHandler<>` against its implemented interface. Decorator types (those that implement `IDecoratorMarker`) are explicitly excluded from handler registration.

Key behaviors:

- **No per-handler wiring** — drop a new handler class into the project and it is picked up automatically on the next run.
- **Internal handlers supported** — scanning uses `publicOnly: false`, so `internal sealed class` handlers are found.
- **One handler per request type** — each request resolves to exactly one handler (plus any decorators wrapped around it). Registering multiple handlers for the same request type is not a supported scenario and will fail during resolution.
- **Dispatcher lifetime** — `IDispatcher` is always registered as `Transient`.
- **Handler lifetime** — handlers default to `Scoped`, which aligns with common ASP.NET Core request-scoped dependencies such as `DbContext`.

---

## Configuration

`AddCustomDispatcher` accepts an optional options delegate and a handler lifetime:

```csharp
public class DispatchOptions
{
    public bool UseLogging    { get; set; } = true;  // wrap every handler in LoggingDecorator
    public bool UseValidation { get; set; } = true;  // wrap every handler in ValidatorDecorator
}
```

The `lifetime` parameter controls handler registrations only. It does not change the `Transient` lifetime of `IDispatcher` itself.

```csharp
// Defaults: logging on, validation on, Scoped handler lifetime
builder.Services.AddCustomDispatcher<Program>();

// Disable the built-in validation decorator
builder.Services.AddCustomDispatcher<Program>(options => options.UseValidation = false);

// Disable both built-in decorators
builder.Services.AddCustomDispatcher<Program>(options =>
{
    options.UseLogging    = false;
    options.UseValidation = false;
});

// Custom handler lifetime
builder.Services.AddCustomDispatcher<Program>(
    configureOptions: options => options.UseLogging = true,
    lifetime: ServiceLifetime.Transient);
```

---

## The decorator pipeline

Decorators add cross-cutting behavior around a handler without changing the handler itself. `IDecorator<TRequest, TResponse>` is a composite interface that extends both `IRequestHandler<,>` and `IDecoratorMarker` — declare one interface, and the scanner never treats the decorator as a handler.

### Built-in decorators

| Decorator | Behavior | Position |
|---|---|---|
| `ValidatorDecorator<,>` | Runs all registered `IValidator<TRequest>` instances (FluentValidation) before the handler; throws `ValidationException` on failure | Inner |
| `LoggingDecorator<,>` | Logs request start, finish, and errors with a per-request logging scope | Outermost |

Resulting pipeline:

```
LoggingDecorator → ValidatorDecorator → YourHandler
```

Concrete registration example:

```csharp
services.Decorate(typeof(IRequestHandler<,>), typeof(ValidatorDecorator<,>));
services.Decorate(typeof(IRequestHandler<,>), typeof(LoggingDecorator<,>));
```

Because Scrutor applies the last registered decorator as the outer wrapper, the request enters `LoggingDecorator` first, then `ValidatorDecorator`, and finally the handler.

### Adding a global custom decorator

A global decorator wraps every handler. Register it with Scrutor's `.Decorate()` after `AddCustomDispatcher`:

```csharp
public class MetricsDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _inner;

    public MetricsDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            return await _inner.Handle(request, cancellationToken);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(start);
            Metrics.RecordHandlerDuration(typeof(TRequest).Name, elapsed);
        }
    }
}

// Registered after AddCustomDispatcher — becomes the new outermost layer
builder.Services.Decorate(typeof(IRequestHandler<,>), typeof(MetricsDecorator<,>));
```

### Adding a targeted decorator

A targeted decorator wraps a single request type only:

```csharp
// Registration
builder.Services.Decorate(
    typeof(IRequestHandler<GetToDosQuery, IEnumerable<ToDo>>),
    typeof(GetToDosQueryCacheDecorator));

// Implementation
internal sealed class GetToDosQueryCacheDecorator : IDecorator<GetToDosQuery, IEnumerable<ToDo>>
{
    private readonly IRequestHandler<GetToDosQuery, IEnumerable<ToDo>> _inner;
    private readonly IMemoryCache _cache;

    public GetToDosQueryCacheDecorator(
        IRequestHandler<GetToDosQuery, IEnumerable<ToDo>> inner,
        IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IEnumerable<ToDo>> Handle(GetToDosQuery request, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue("todos", out IEnumerable<ToDo>? cached))
            return cached!;

        var result = await _inner.Handle(request, cancellationToken);
        _cache.Set("todos", result, TimeSpan.FromMinutes(1));
        return result;
    }
}
```

---

## Validation

Validators are plain FluentValidation classes. Register them alongside the dispatcher; `ValidatorDecorator` resolves them automatically:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddCustomDispatcher<Program>();
```

If `UseValidation` is left enabled but no validators are registered for a request type, the `ValidatorDecorator` simply passes the request through.

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

public class CreateToDoCommandValidator : AbstractValidator<CreateToDoCommand>
{
    public CreateToDoCommandValidator()
    {
        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(200).WithMessage("Description must be 200 characters or fewer.");
    }
}
```

## Operational constraints

- **Notifications are out of scope** — this library does not implement `INotification` / `INotificationHandler<T>` semantics.
- **Single-handler resolution** — requests are modeled as one request to one handler, optionally wrapped by decorators.

---

## Complete Program.cs setup

```csharp
builder.Services.AddSingleton<ToDoRepository>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddCustomDispatcher<Program>();        // scans + registers all handlers

// Optional: global custom decorator
builder.Services.Decorate(typeof(IRequestHandler<,>), typeof(MetricsDecorator<,>));

// Optional: targeted decorator
builder.Services.Decorate(
    typeof(IRequestHandler<GetToDosQuery, IEnumerable<ToDo>>),
    typeof(GetToDosQueryCacheDecorator));
```

---
