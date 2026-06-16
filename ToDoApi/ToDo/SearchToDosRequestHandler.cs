using Dispatcher.Contracts;
using Dispatcher.Handlers;
using FluentValidation;

namespace ToDoApi.ToDo;

public sealed record SearchToDosRequest(string Term) : IRequest<IEnumerable<ToDo>>;

public sealed class SearchToDosRequestValidator : AbstractValidator<SearchToDosRequest>
{
    public SearchToDosRequestValidator()
    {
        RuleFor(r => r.Term).NotEmpty().WithMessage("Search term is required.");
    }
}

internal sealed class SearchToDosRequestHandler : IRequestHandler<SearchToDosRequest, IEnumerable<ToDo>>
{
    private readonly ToDoRepository _repository;

    public SearchToDosRequestHandler(ToDoRepository repository) => _repository = repository;

    public Task<IEnumerable<ToDo>> Handle(SearchToDosRequest request, CancellationToken cancellationToken)
        => Task.FromResult(_repository.GetAll()
            .Where(t => t.Description.Contains(request.Term, StringComparison.OrdinalIgnoreCase)));
}
