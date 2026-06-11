namespace ToDoApi.ToDo;

internal sealed class ToDoRepository
{
    private readonly List<ToDo> _todos = [];

    public void Add(ToDo todo) => _todos.Add(todo);

    public ToDo? GetById(Guid id) => _todos.FirstOrDefault(t => t.Id == id);

    public IEnumerable<ToDo> GetAll() => _todos;

    public void Complete(Guid id)
    {
        var todo = GetById(id);
        
        if (todo is null) return;
        
        todo.Complete();
    }
}
