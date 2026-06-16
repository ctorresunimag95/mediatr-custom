using Dispatcher.Contracts;
using Dispatcher.Handlers;

namespace ToDoApi.ToDo;

public sealed record DeleteToDoRequest(Guid Id) : IRequest;

internal sealed class DeleteToDoRequestHandler : IRequestHandler<DeleteToDoRequest>
{
    private readonly ToDoRepository _repository;

    public DeleteToDoRequestHandler(ToDoRepository repository) => _repository = repository;

    public Task Handle(DeleteToDoRequest request, CancellationToken cancellationToken)
    {
        _repository.Remove(request.Id);
        return Task.CompletedTask;
    }
}
