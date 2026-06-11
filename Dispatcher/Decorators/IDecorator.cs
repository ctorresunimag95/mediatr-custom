using Dispatcher.Contracts;
using Dispatcher.Handlers;

namespace Dispatcher.Decorators;

public interface IDecorator<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>, IDecoratorMarker
    where TRequest : IRequest<TResponse>
{
}

public interface IDecorator<in TRequest> : IRequestHandler<TRequest>, IDecoratorMarker
    where TRequest : IRequest
{
}
