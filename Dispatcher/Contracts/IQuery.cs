namespace Dispatcher.Contracts;

/// <summary>
/// Interface for a query that returns a response
/// </summary>
/// <typeparam name="T">Type of the response object</typeparam>
public interface IQuery<out T> : IRequest<T>
{
}
