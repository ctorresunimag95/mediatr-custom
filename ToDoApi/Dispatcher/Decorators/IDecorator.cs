using ToDoApi.Dispatcher.Contracts;
using ToDoApi.Dispatcher.Handlers;

namespace ToDoApi.Dispatcher.Decorators;

public interface IDecorator<in TRequest, TResponse> : IRequestHandler<TRequest, TResponse>, IDecoratorMarker
    where TRequest : IRequest<TResponse>
{
}

public interface IDecorator<in TRequest> : IRequestHandler<TRequest>, IDecoratorMarker
    where TRequest : IRequest
{
}
