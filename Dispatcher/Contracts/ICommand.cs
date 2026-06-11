namespace Dispatcher.Contracts;

/// <summary>
/// Interface for a command that returns a response
/// </summary>
/// <typeparam name="T">Type of the response object</typeparam>
public interface ICommand<out T> : IRequest<T>
{
}

/// <summary>
/// Interface for a command with no response
/// </summary>
public interface ICommand : IRequest
{
}
