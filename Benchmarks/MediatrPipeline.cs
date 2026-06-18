using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

// MediatR equivalents of the custom LoggingDecorator / ValidatorDecorator,
// so the "decorated" scenario compares like-for-like pipelines.

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        KeyValuePair<string, object>[] scopeValues =
        [
            new("Request", typeof(TRequest).Name),
        ];
        using var scope = _logger.BeginScope(scopeValues);

        _logger.LogInformation("Started handling message {MessageType}", typeof(TRequest).Name);

        try
        {
            return await next(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while handling message {MessageType}", typeof(TRequest).Name);
            throw;
        }
        finally
        {
            _logger.LogInformation("Finished handling message {MessageType}", typeof(TRequest).Name);
        }
    }
}

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(request, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
