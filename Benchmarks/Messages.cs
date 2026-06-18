using FluentValidation;
using CustomContracts = Dispatcher.Contracts;
using CustomHandlers = Dispatcher.Handlers;

namespace Benchmarks;

public sealed record Pong(int Value);

// ---- Custom dispatcher message ----
// Implements the dispatcher's own IRequest<TResponse> to mirror MediatR as closely as possible.
public sealed record CustomPingRequest(int Value) : CustomContracts.IRequest<Pong>;

internal sealed class CustomPingRequestHandler : CustomHandlers.IRequestHandler<CustomPingRequest, Pong>
{
    public Task<Pong> Handle(CustomPingRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new Pong(request.Value));
}

public sealed class CustomPingRequestValidator : AbstractValidator<CustomPingRequest>
{
    public CustomPingRequestValidator()
    {
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    }
}

// Minimal void handler so AddCustomDispatcher's Decorate(IRequestHandler<>) has a
// registration to decorate when the decorated scenario is enabled. Not benchmarked.
public sealed record CustomNoopRequest : CustomContracts.IRequest;

internal sealed class CustomNoopRequestHandler : CustomHandlers.IRequestHandler<CustomNoopRequest>
{
    public Task Handle(CustomNoopRequest request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// ---- MediatR message ----
public sealed record MediatrPingRequest(int Value) : MediatR.IRequest<Pong>;

internal sealed class MediatrPingRequestHandler : MediatR.IRequestHandler<MediatrPingRequest, Pong>
{
    public Task<Pong> Handle(MediatrPingRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new Pong(request.Value));
}

public sealed class MediatrPingRequestValidator : AbstractValidator<MediatrPingRequest>
{
    public MediatrPingRequestValidator()
    {
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    }
}
