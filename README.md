# mediatr-custom

A small, self-contained mediator / dispatcher for .NET — a free alternative to [MediatR](https://github.com/jbogard/MediatR), which now requires a commercial license for most usage.

It keeps the parts most teams actually rely on:

- A single `IDispatcher` interface (with a `Dispatcher` implementation) that routes commands and queries to their handlers.
- **Automatic handler registration** via assembly scanning (no manual `AddScoped` per handler).
- A pluggable **decorator pipeline** (logging, validation, and your own cross-cutting concerns).
- A clean separation between **commands** (state changes) and **queries** (reads).

The [`ToDoApi`](ToDoApi/) project is a working reference implementation.

## Why this exists

MediatR moved to a paid license, so this repo provides a drop-in-style replacement built on familiar concepts (`IRequest`, `IRequestHandler<,>`, a `Send`-style dispatcher) without the dependency or the license. It is intentionally minimal — read it, copy it, and adapt it to your project.

## Repository structure

```
mediatr-custom/
├── Dispatcher/        # Class library (net10.0) — the reusable dispatcher
└── ToDoApi/           # ASP.NET Core 10 reference implementation
```

## Installation

Reference the `Dispatcher` project (or publish it as a NuGet package) and add it to your project:

```xml
<ItemGroup>
  <ProjectReference Include="../Dispatcher/Dispatcher.csproj" />
</ItemGroup>
```

The library brings its own dependencies:

| Package | Version | Purpose |
| --- | --- | --- |
| `FluentValidation` | 12.x | Used by the built-in `ValidatorDecorator` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.x | `IServiceCollection`, `IServiceProvider` |
| `Microsoft.Extensions.Logging.Abstractions` | 10.x | `ILogger<T>` used by `LoggingDecorator` |

## Core concepts

| Concept | Interface | Purpose |
| --- | --- | --- |
| Dispatcher | `IDispatcher` | Entry point you inject; routes a request to its handler via `SendAsync` |
| Command (with response) | `ICommand<TResponse>` | A request that changes state and returns a result |
| Command (no response) | `ICommand` | A request that changes state and returns nothing |
| Query | `IQuery<TResponse>` | A read-only request that returns a result |
| Handler | `ICommandHandler<,>`, `ICommandHandler<>`, `IQueryHandler<,>` | Handles exactly one request type |
| Decorator | `IDecorator<TRequest, TResponse>` / `IDecorator<TRequest>` | Wraps a handler to add cross-cutting behavior |

`ICommand`, `IQuery`, and the handler interfaces all derive from the generic `IRequest` / `IRequestHandler<,>` contracts, which is what the dispatcher and the scanner key off of.

### Defining a command and handler

```csharp
// 1. Define the request
public sealed record CreateToDoCommand(string Description) : ICommand<CreateToDoResponse>;

public sealed record CreateToDoResponse(Guid Id, string Description, bool IsCompleted);

// 2. Define its handler — discovered and registered automatically
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

### Dispatching

Depend on the `IDispatcher` **interface** (not the concrete type) and call `SendAsync`. The correct overload is chosen by whether the request is an `ICommand`, `ICommand<T>`, or `IQuery<T>`:

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
```

The interface keeps call sites decoupled from the implementation and makes the dispatcher easy to mock in tests:

```csharp
public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);
    Task SendAsync(ICommand command, CancellationToken cancellationToken);
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);
}
```

## Automatic handler registration

Registration is one call in `Program.cs`. Pass one or more assembly marker types — one per project that contains handlers:

```csharp
// Single assembly (convenience generic overload)
builder.Services.AddCustomDispatcher<Program>();

// Multiple assemblies — handlers spread across several projects
builder.Services.AddCustomDispatcher([typeof(Program), typeof(OtherProjectMarker)]);
```

`AddCustomDispatcher` scans every assembly represented by the marker types and registers **every** class that implements `IRequestHandler<,>` or `IRequestHandler<>` against its implemented interface. Decorator types (those that implement `IDecoratorMarker`, including those that use the `IDecorator<,>` composite) are explicitly excluded so they don't get picked up as handlers. Duplicate assemblies (two markers from the same assembly) are silently deduplicated.

Key behaviors:

- **No per-handler wiring** — drop a new handler into any scanned project and it is registered on the next run.
- **Internal handlers are supported** — scanning uses `publicOnly: false`, so `internal sealed class` handlers are found.
- **One handler per request type** — each request resolves to a single handler (plus any decorators wrapped around it).
- The dispatcher is registered as transient against the interface: `AddTransient<IDispatcher, ...>()`.

## Configuration

`AddCustomDispatcher` accepts an options delegate and a service lifetime.

### Options

```csharp
public class DispatchOptions
{
    public bool UseLogging { get; set; } = true;     // wrap every handler in LoggingDecorator
    public bool UseValidation { get; set; } = true;  // wrap every handler in ValidatorDecorator
}
```

```csharp
// Defaults: logging on, validation on, Scoped lifetime
builder.Services.AddCustomDispatcher<Program>();

// Multiple assemblies with options
builder.Services.AddCustomDispatcher(
    [typeof(Program), typeof(OtherProjectMarker)],
    options => options.UseValidation = false);

// Turn off the built-in validation decorator
builder.Services.AddCustomDispatcher<Program>(options => options.UseValidation = false);

// Disable both built-in decorators
builder.Services.AddCustomDispatcher<Program>(options =>
{
    options.UseLogging = false;
    options.UseValidation = false;
});
```

### Handler lifetime

The `lifetime` parameter controls the lifetime used when registering handlers (defaults to `Scoped`):

```csharp
builder.Services.AddCustomDispatcher<Program>(
    configureOptions: options => options.UseLogging = true,
    lifetime: ServiceLifetime.Transient);

// Multi-assembly with custom lifetime
builder.Services.AddCustomDispatcher(
    [typeof(Program), typeof(OtherProjectMarker)],
    lifetime: ServiceLifetime.Transient);
```

## The decorator pipeline

Decorators add cross-cutting behavior around a handler without changing the handler itself. A decorator implements `IDecorator<TRequest, TResponse>` (or `IDecorator<TRequest>` for void requests) and takes the inner handler as a constructor dependency.

`IDecorator<,>` is a composite interface that extends both `IRequestHandler<,>` and `IDecoratorMarker`, so you only need to declare one interface — no need to list both separately.

Two decorators ship out of the box and are applied based on `DispatchOptions`:

- **`ValidatorDecorator<,>`** — runs all registered FluentValidation `IValidator<TRequest>` instances before the handler and throws a `ValidationException` on failure. (Applied first.)
- **`LoggingDecorator<,>`** — logs start/finish/errors around the handler, with a logging scope per request. (Applied last, so it wraps validation.)

Because validation is registered before logging, the resulting pipeline is:

```
LoggingDecorator → ValidatorDecorator → YourHandler
```

### Validation

Validators are plain FluentValidation classes, registered the usual way; the decorator picks them up automatically:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

public class CreateToDoCommandValidator : AbstractValidator<CreateToDoCommand>
{
    public CreateToDoCommandValidator()
    {
        RuleFor(c => c.Description).NotEmpty().WithMessage("Description is required.");
    }
}
```

### Adding your own decorators

**Global decorator** (wraps every handler) — implement `IDecorator<,>` and register it with `Decorate` after `AddCustomDispatcher`:

```csharp
public class MyMetricsDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _inner;
    public MyMetricsDecorator(IRequestHandler<TRequest, TResponse> inner) => _inner = inner;

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        // ... metrics logic ...
        return await _inner.Handle(request, cancellationToken);
    }
}

builder.Services.AddCustomDispatcher<Program>();
builder.Services.Decorate(typeof(IRequestHandler<,>), typeof(MyMetricsDecorator<,>));
```

**Targeted decorator** (wraps a single request type) — decorate just that closed interface:

```csharp
builder.Services.Decorate(
    typeof(IRequestHandler<GetToDosQuery, IEnumerable<ToDo>>),
    typeof(GetToDosQueryHandlerDecorator));
```

```csharp
internal sealed class GetToDosQueryHandlerDecorator
    : IDecorator<GetToDosQuery, IEnumerable<ToDo>>
{
    private readonly IRequestHandler<GetToDosQuery, IEnumerable<ToDo>> _inner;
    private readonly ILogger<GetToDosQueryHandlerDecorator> _logger;

    public GetToDosQueryHandlerDecorator(
        IRequestHandler<GetToDosQuery, IEnumerable<ToDo>> inner,
        ILogger<GetToDosQueryHandlerDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<IEnumerable<ToDo>> Handle(GetToDosQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetToDosQuery.");
        return await _inner.Handle(request, cancellationToken);
    }
}
```

> `IDecorator<,>` already implies `IDecoratorMarker`, so the scanner will never treat it as a real handler.

## Putting it together

A complete `Program.cs` setup:

```csharp
builder.Services.AddSingleton<ToDoRepository>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Single assembly
builder.Services.AddCustomDispatcher<Program>();

// Or across multiple assemblies
builder.Services.AddCustomDispatcher([typeof(Program), typeof(OtherProjectMarker)]);

builder.Services.Decorate(                              // optional targeted decorator
    typeof(IRequestHandler<GetToDosQuery, IEnumerable<ToDo>>),
    typeof(GetToDosQueryHandlerDecorator));
```

## Dependencies

- [FluentValidation](https://docs.fluentvalidation.net/) 12.x — used by the validation decorator (optional; disable via `UseValidation = false`).
- [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) 10.x — DI abstractions.
- [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions) 10.x — logging abstractions.
- .NET 10.

## Running the sample

```bash
cd ToDoApi
dotnet run
```

Then exercise the endpoints: `POST /todos`, `POST /todos/{id}/complete`, and `GET /todos`.
