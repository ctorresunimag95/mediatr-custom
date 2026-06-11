using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ToDoApi;
using ToDoApi.Dispatcher;
using ToDoApi.Dispatcher.Handlers;
using ToDoApi.ErrorHandlers;
using ToDoApi.ToDo;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<ToDoRepository>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddCustomDispatcher();
builder.Services.Decorate(typeof(IRequestHandler<GetToDosQuery, IEnumerable<ToDo>>), typeof(GetToDosQueryHandlerDecorator));


builder.Services
    .AddProblemDetails(options =>
        options.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Extensions.Add("trace-id", ctx.HttpContext.TraceIdentifier);
            ctx.ProblemDetails.Extensions.Add("instance", $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}");
        });

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalErrorHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapOpenApi();

app.UseStatusCodePages();
app.UseExceptionHandler();

app.MapPost("/todos", async ([FromBody] CreateToDoCommand command
    , [FromServices] IDispatcher dispatcher
    , CancellationToken cancellationToken) =>
{
    var response = await dispatcher.SendAsync(command, cancellationToken);
    return Results.Created($"/todos/{response.Id}", response);
});

app.MapPost("/todos/{id:guid}/complete", async ([FromRoute] Guid id
    , [FromServices] IDispatcher dispatcher
    , CancellationToken cancellationToken) =>
{
    await dispatcher.SendAsync(new CompleteTodoCommand(id), cancellationToken);
    return Results.NoContent();
});

app.MapGet("/todos", async ([FromServices] IDispatcher dispatcher
    , CancellationToken cancellationToken) =>
{
    var todos = await dispatcher.SendAsync(new GetToDosQuery(), cancellationToken);
    return Results.Ok(todos);
});

app.Run();
