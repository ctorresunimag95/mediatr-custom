using Microsoft.AspNetCore.Mvc;
using ToDoApi.Dispatcher;
using ToDoApi.ToDo;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<ToDoRepository>();

builder.Services.AddCustomDispatcher();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapOpenApi();

app.MapPost("/todos", async ([FromBody] CreateToDoCommand command
    , [FromServices] Dispatcher dispatcher
    , CancellationToken cancellationToken) =>
{
    var response = await dispatcher.SendAsync(command, cancellationToken);
    return Results.Created($"/todos/{response.Id}", response);
});

app.MapPost("/todos/{id:guid}/complete", async ([FromRoute] Guid id
    , [FromServices] Dispatcher dispatcher
    , CancellationToken cancellationToken) =>
{
    await dispatcher.SendAsync(new CompleteTodoCommand(id), cancellationToken);
    return Results.NoContent();
});

app.MapGet("/todos", async ([FromServices] Dispatcher dispatcher
    , CancellationToken cancellationToken) =>
{
    var todos = await dispatcher.SendAsync(new GetToDosQuery(), cancellationToken);
    return Results.Ok(todos);
});

app.Run();
