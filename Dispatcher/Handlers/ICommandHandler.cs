using Dispatcher.Contracts;

namespace Dispatcher.Handlers;

/// <summary>
/// Interface for a command handler that returns a response of the provided type
/// </summary>
/// <typeparam name="TCommand">The command to handle</typeparam>
/// <typeparam name="TResponse">The type of the response</typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}

/// <summary>
/// Interface for a handler that does not return a response
/// </summary>
/// <typeparam name="TCommand">The command to handle</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand
{
}
