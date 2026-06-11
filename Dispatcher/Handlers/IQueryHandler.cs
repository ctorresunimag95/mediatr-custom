using Dispatcher.Contracts;

namespace Dispatcher.Handlers;

/// <summary>
/// Interface for a query handler that returns a response of the provided type
/// </summary>
/// <typeparam name="TQuery">The query to handle</typeparam>
/// <typeparam name="TResponse">The type of the response</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
