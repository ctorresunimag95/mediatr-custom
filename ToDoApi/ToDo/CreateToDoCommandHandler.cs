using FluentValidation;
using ToDoApi.Dispatcher.Contracts;
using ToDoApi.Dispatcher.Handlers;

namespace ToDoApi.ToDo;

public sealed record CreateToDoCommand(string Description) : ICommand<CreateToDoResponse>;

public sealed record CreateToDoResponse(Guid Id, string Description, bool IsCompleted);

public sealed class ToDo
{
    public Guid Id { get; }

    public string Description { get; private set; }

    public bool IsCompleted { get; private set; }

    public ToDo(string description)
    {
        Id = Guid.NewGuid();
        Description = description;
    }

    public void Complete() => IsCompleted = true;
}

public class CreateToDoCommandValidator : AbstractValidator<CreateToDoCommand>
{
    public CreateToDoCommandValidator()
    {
        RuleFor(c => c.Description).NotEmpty().WithMessage("Description is required.");
    }
}

internal sealed class CreateToDoCommandHandler : ICommandHandler<CreateToDoCommand, CreateToDoResponse>
{
    private readonly ToDoRepository _toDoRepository;

    public CreateToDoCommandHandler(ToDoRepository toDoRepository)
    {
        _toDoRepository = toDoRepository;
    }

    public Task<CreateToDoResponse> Handle(CreateToDoCommand command, CancellationToken cancellationToken)
    {
        var todo = new ToDo(command.Description);

        _toDoRepository.Add(todo);

        return Task.FromResult(new CreateToDoResponse(todo.Id, todo.Description, todo.IsCompleted));
    }
}
