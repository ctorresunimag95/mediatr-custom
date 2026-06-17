# Migration Guide — MediatR to Custom Dispatcher

This guide walks through migrating an existing .NET project from MediatR to the custom dispatcher library. Each section shows the MediatR pattern on the left and the equivalent custom dispatcher pattern on the right.

---

## Overview of changes

| Area | MediatR | Custom Dispatcher |
|---|---|---|
| Package | `MediatR` + `MediatR.Extensions.Microsoft.DependencyInjection` | `Dispatcher` project reference |
| DI registration | `builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))` | `builder.Services.AddCustomDispatcher<TMarker>()` |
| Dispatcher interface | `IMediator` | `IDispatcher` |
| Dispatch method | `_mediator.Send(request, ct)` | `_dispatcher.SendAsync(request, ct)` |
| Request marker (command) | `IRequest<TResponse>` | `ICommand<TResponse>` *(or keep `IRequest<T>`)* |
| Request marker (void command) | `IRequest` | `ICommand` *(or keep `IRequest`)* |
| Request marker (query) | `IRequest<TResponse>` (convention-based) | `IQuery<TResponse>` *(or keep `IRequest<T>`)* |
| Handler interface | `IRequestHandler<TRequest, TResponse>` | `ICommandHandler<TRequest, TResponse>` or `IQueryHandler<TRequest, TResponse>` *(or keep `IRequestHandler<,>`)* |
| Void handler interface | `IRequestHandler<TRequest>` | `ICommandHandler<TRequest>` *(or keep `IRequestHandler<>`)* |
| Pipeline behavior | `IPipelineBehavior<TRequest, TResponse>` | `IDecorator<TRequest, TResponse>` |
| Validation | `ValidationBehavior` (custom) + FluentValidation | `ValidatorDecorator` built-in + FluentValidation |
| Notifications / events | `INotification` + `INotificationHandler<T>` | Not included — use a dedicated event bus if needed |

---

## Step 1 — Replace packages

Remove MediatR packages from your `.csproj`:

```xml
<!-- Before -->
<PackageReference Include="MediatR" Version="12.x" />
<PackageReference Include="MediatR.Extensions.Microsoft.DependencyInjection" Version="12.x" />
```

Add the dispatcher project reference:

```xml
<!-- After -->
<ProjectReference Include="../Dispatcher/Dispatcher.csproj" />

<!-- Or install nuget package when created -->
<PackageReference Include="Dispatcher" Version="1.0.0" />
```

---

## Step 2 — Update DI registration

```csharp
// Before
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// After
builder.Services.AddCustomDispatcher<Program>();
```

If you were passing options to MediatR (e.g. a custom `MediatorServiceConfiguration`), map them to `DispatchOptions`:

```csharp
// After — with options
builder.Services.AddCustomDispatcher<Program>(options =>
{
    options.UseLogging    = true;
    options.UseValidation = true;
});
```

`AddCustomDispatcher` keeps `IDispatcher` registered as `Transient`. The optional `lifetime` parameter controls handler registrations only and defaults to `Scoped`.

---

## Step 3 — Update request definitions

> **`IRequest` is now supported directly.** The dispatcher accepts `IRequest<T>` and `IRequest` as-is — renaming to `ICommand`/`IQuery` is **optional**. You can skip this step entirely for a minimal-effort migration and adopt the semantic interfaces incrementally, or keep `IRequest` permanently if preferred.

### Option A — Keep IRequest (minimal migration)

No changes required. Handlers implementing `IRequestHandler<,>` or `IRequestHandler<>` are discovered and decorated automatically, and `IDispatcher.SendAsync` accepts both overloads:

```csharp
// Unchanged from MediatR — works as-is
public sealed record CreateToDoCommand(string Description) : IRequest<CreateToDoResponse>;
public sealed record CompleteTodoCommand(Guid Id) : IRequest;
public sealed record GetToDosQuery() : IRequest<IEnumerable<ToDo>>;
```

### Option B — Adopt semantic interfaces (recommended for new code)

Rename to `ICommand<T>`, `ICommand`, or `IQuery<T>` to make intent explicit in the type system:

### Command with a result

```csharp
// Before
public sealed record CreateToDoCommand(string Description) : IRequest<CreateToDoResponse>;

// After
public sealed record CreateToDoCommand(string Description) : ICommand<CreateToDoResponse>;
```

### Void command

```csharp
// Before
public sealed record CompleteTodoCommand(Guid Id) : IRequest;

// After
public sealed record CompleteTodoCommand(Guid Id) : ICommand;
```

### Query

MediatR does not distinguish queries from commands at the type level. The custom dispatcher introduces `IQuery<T>` to make reads explicit:

```csharp
// Before
public sealed record GetToDosQuery() : IRequest<IEnumerable<ToDo>>;

// After
public sealed record GetToDosQuery() : IQuery<IEnumerable<ToDo>>;
```

---

## Step 4 — Update handler definitions

### Command handler (with result)

```csharp
// Before
public class CreateToDoCommandHandler : IRequestHandler<CreateToDoCommand, CreateToDoResponse>
{
    public Task<CreateToDoResponse> Handle(CreateToDoCommand request, CancellationToken cancellationToken) { ... }
}

// After
internal sealed class CreateToDoCommandHandler : ICommandHandler<CreateToDoCommand, CreateToDoResponse>
{
    public Task<CreateToDoResponse> Handle(CreateToDoCommand command, CancellationToken cancellationToken) { ... }
}
```

### Void command handler

```csharp
// Before
public class CompleteTodoCommandHandler : IRequestHandler<CompleteTodoCommand>
{
    public Task Handle(CompleteTodoCommand request, CancellationToken cancellationToken) { ... }
}

// After
internal sealed class CompleteTodoCommandHandler : ICommandHandler<CompleteTodoCommand>
{
    public Task Handle(CompleteTodoCommand command, CancellationToken cancellationToken) { ... }
}
```

### Query handler

```csharp
// Before
public class GetToDosQueryHandler : IRequestHandler<GetToDosQuery, IEnumerable<ToDo>>
{
    public Task<IEnumerable<ToDo>> Handle(GetToDosQuery request, CancellationToken cancellationToken) { ... }
}

// After
internal sealed class GetToDosQueryHandler : IQueryHandler<GetToDosQuery, IEnumerable<ToDo>>
{
    public Task<IEnumerable<ToDo>> Handle(GetToDosQuery query, CancellationToken cancellationToken) { ... }
}
```

---

## Step 5 — Update call sites

Replace `IMediator` with `IDispatcher` at injection points and rename `Send` to `SendAsync`:

```csharp
// Before
public class TodosController
{
    private readonly IMediator _mediator;
    public TodosController(IMediator mediator) => _mediator = mediator;

    public async Task<IActionResult> Create(CreateToDoCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}

// After
public class TodosController
{
    private readonly IDispatcher _dispatcher;
    public TodosController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<IActionResult> Create(CreateToDoCommand command, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(command, ct);
        return Ok(result);
    }
}
```

For minimal API endpoints:

```csharp
// Before
app.MapPost("/todos", async ([FromBody] CreateToDoCommand cmd, IMediator mediator, CancellationToken ct)
    => Results.Ok(await mediator.Send(cmd, ct)));

// After
app.MapPost("/todos", async ([FromBody] CreateToDoCommand cmd, IDispatcher dispatcher, CancellationToken ct)
    => Results.Ok(await dispatcher.SendAsync(cmd, ct)));
```

---

## Step 6 — Migrate pipeline behaviors to decorators

MediatR pipeline behaviors (`IPipelineBehavior<,>`) become `IDecorator<,>` implementations.

### Global behavior (all requests)

```csharp
// Before — MediatR IPipelineBehavior
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}

// Registered as:
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

```csharp
// After — IDecorator
public class LoggingDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _inner;
    private readonly ILogger<LoggingDecorator<TRequest, TResponse>> _logger;

    public LoggingDecorator(
        IRequestHandler<TRequest, TResponse> inner,
        ILogger<LoggingDecorator<TRequest, TResponse>> logger)
    {
        _inner  = inner;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await _inner.Handle(request, cancellationToken);
        _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}

// Registered as (after AddCustomDispatcher):
builder.Services.Decorate(typeof(IRequestHandler<,>), typeof(LoggingDecorator<,>));
```

### Key difference

MediatR behaviors use a `next()` delegate to call the inner handler. Decorators hold a reference to the inner `IRequestHandler<,>` and call `_inner.Handle(...)` directly. The inner handler (or next decorator) is injected via the constructor, not passed as a parameter.

---

## Step 7 — Migrate validation behavior

If you were using a custom `ValidationBehavior<TRequest, TResponse>` that ran FluentValidation, replace it with the built-in `ValidatorDecorator` — it is enabled by default.

```csharp
// Before — custom ValidationBehavior registered manually
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    // ... manual validation logic ...
}

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

```csharp
// After — register validators and the dispatcher; ValidatorDecorator is on by default
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddCustomDispatcher<Program>(); // UseValidation = true by default
```

Validator classes themselves do not change — they remain standard `AbstractValidator<T>` implementations.

---

## Step 8 — Notifications (INotification)

The custom dispatcher does not include a notification / event bus. If your project uses `INotification` + `INotificationHandler<T>`, you have two options:

1. **Keep MediatR only for notifications** — reference both libraries temporarily and migrate notifications separately.
2. **Replace with a dedicated event bus** — use a lightweight pub/sub mechanism (e.g. a simple `List<IEventHandler<T>>` resolved from DI, or an infrastructure-level message bus).

This is the only MediatR concept without a direct equivalent in the custom dispatcher.

---

## Quick reference — rename cheatsheet

Rows marked *(optional)* can be skipped — `IRequest` and `IRequestHandler` are supported natively and you can keep them as-is.

| Find (MediatR) | Replace with (Custom Dispatcher) |
|---|---|
| `using MediatR;` | `using Dispatcher;` (adjust to your namespace) |
| `: IRequest<T>` | `: ICommand<T>` or `: IQuery<T>` *(optional — keep as-is if preferred)* |
| `: IRequest` | `: ICommand` *(optional — keep as-is if preferred)* |
| `: IRequestHandler<TReq, TRes>` | `: ICommandHandler<TReq, TRes>` or `: IQueryHandler<TReq, TRes>` *(optional — keep as-is if preferred)* |
| `: IRequestHandler<TReq>` | `: ICommandHandler<TReq>` *(optional — keep as-is if preferred)* |
| `: IPipelineBehavior<TReq, TRes>` | `: IDecorator<TReq, TRes>` |
| `IMediator` | `IDispatcher` |
| `_mediator.Send(` | `_dispatcher.SendAsync(` |
| `await next()` (inside behavior) | `await _inner.Handle(request, ct)` |
| `AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))` | `AddCustomDispatcher<TMarker>()` |
| `AddTransient(typeof(IPipelineBehavior<,>), typeof(MyBehavior<,>))` | `Decorate(typeof(IRequestHandler<,>), typeof(MyDecorator<,>))` |
