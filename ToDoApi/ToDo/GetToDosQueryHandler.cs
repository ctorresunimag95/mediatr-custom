using Dispatcher.Contracts;
using Dispatcher.Decorators;
using Dispatcher.Handlers;

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

internal sealed class GetToDosQueryHandlerDecorator : IDecorator<GetToDosQuery, IEnumerable<ToDo>>
{
    private readonly IRequestHandler<GetToDosQuery, IEnumerable<ToDo>> _innerHandler;
    private readonly ILogger<GetToDosQueryHandlerDecorator> _logger;

    public GetToDosQueryHandlerDecorator(IRequestHandler<GetToDosQuery, IEnumerable<ToDo>> innerHandler
        , ILogger<GetToDosQueryHandlerDecorator> logger)
    {
        _innerHandler = innerHandler;
        _logger = logger;
    }

    public async Task<IEnumerable<ToDo>> Handle(GetToDosQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetToDosQuery from GetToDosQueryHandlerDecorator.");

        return await _innerHandler.Handle(request, cancellationToken);
    }
}