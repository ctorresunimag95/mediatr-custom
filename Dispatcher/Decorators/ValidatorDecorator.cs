using Dispatcher.Contracts;
using Dispatcher.Handlers;
using FluentValidation;

namespace Dispatcher.Decorators;

public class ValidatorDecorator<TRequest, TResponse> : IDecorator<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse> _innerHandler;
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidatorDecorator(IRequestHandler<TRequest, TResponse> innerHandler
        , IEnumerable<IValidator<TRequest>> validators)
    {
        _innerHandler = innerHandler;
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(request, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await _innerHandler.Handle(request, cancellationToken);
    }
}

public class ValidatorDecorator<TRequest> : IDecorator<TRequest>
    where TRequest : IRequest
{
    private readonly IRequestHandler<TRequest> _innerHandler;
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidatorDecorator(IRequestHandler<TRequest> innerHandler
        , IEnumerable<IValidator<TRequest>> validators)
    {
        _innerHandler = innerHandler;
        _validators = validators;
    }

    public async Task Handle(TRequest request, CancellationToken cancellationToken)
    {
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(request, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        await _innerHandler.Handle(request, cancellationToken);
    }
}
