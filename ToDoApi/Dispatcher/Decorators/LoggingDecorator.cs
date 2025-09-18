using ToDoApi.Dispatcher.Contracts;
using ToDoApi.Dispatcher.Handlers;

namespace ToDoApi.Dispatcher.Decorators;

public class LoggingDecorator<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _innerHandler;
    private readonly ILogger<LoggingDecorator<TRequest, TResponse>> _logger;

    public LoggingDecorator(IRequestHandler<TRequest, TResponse> innerHandler
        , ILogger<LoggingDecorator<TRequest, TResponse>> logger)
    {
        _innerHandler = innerHandler;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        KeyValuePair<string, object>[] scopeValues =
        [
            new("Request", typeof(TRequest).Name),
        ];
        using var scope = _logger.BeginScope(scopeValues);

        _logger.LogInformation("Started handling message {MessageType}", typeof(TRequest).Name);

        var response = await _innerHandler.Handle(request, cancellationToken);

        _logger.LogInformation("Finished handling message {MessageType}", typeof(TRequest).Name);

        return response;
    }
}

public class LoggingDecorator<TRequest> : IRequestHandler<TRequest>
    where TRequest : IRequest
{
    private readonly IRequestHandler<TRequest> _innerHandler;
    private readonly ILogger<LoggingDecorator<TRequest>> _logger;

    public LoggingDecorator(IRequestHandler<TRequest> innerHandler
        , ILogger<LoggingDecorator<TRequest>> logger)
    {
        _innerHandler = innerHandler;
        _logger = logger;
    }

    public async Task Handle(TRequest request, CancellationToken cancellationToken)
    {
        KeyValuePair<string, object>[] scopeValues =
        [
            new("Request", typeof(TRequest).Name),
        ];
        using var scope = _logger.BeginScope(scopeValues);

        _logger.LogInformation("Started handling message {MessageType}", typeof(TRequest).Name);

        await _innerHandler.Handle(request, cancellationToken);

        _logger.LogInformation("Finished handling message {MessageType}", typeof(TRequest).Name);
    }
}
