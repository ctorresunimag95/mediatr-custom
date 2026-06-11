using ToDoApi.Dispatcher.Contracts;
using ToDoApi.Dispatcher.Handlers;

namespace ToDoApi.ToDo;

public sealed record GetToDosQuery : IQuery<IEnumerable<ToDo>>;

internal sealed class GetToDosQueryHandler : IQueryHandler<GetToDosQuery, IEnumerable<ToDo>>
{
    private readonly ToDoRepository _toDoRepository;
    public GetToDosQueryHandler(ToDoRepository toDoRepository)
    {
        _toDoRepository = toDoRepository;
    }
    public Task<IEnumerable<ToDo>> Handle(GetToDosQuery request, CancellationToken cancellationToken)
    {
        var todos = _toDoRepository.GetAll();
        return Task.FromResult(todos);
    }
}