using Dispatcher.Contracts;
using Dispatcher.Handlers;

namespace ToDoApi.ToDo;

public sealed record CompleteTodoCommand(Guid Id) : ICommand;

internal sealed class CompleteTodoCommandHandler : ICommandHandler<CompleteTodoCommand>
{
    private readonly ToDoRepository _toDoRepository;

    public CompleteTodoCommandHandler(ToDoRepository toDoRepository)
    {
        _toDoRepository = toDoRepository;
    }

    public Task Handle(CompleteTodoCommand request, CancellationToken cancellationToken)
    {
        _toDoRepository.Complete(request.Id);

        return Task.CompletedTask;
    }
}
