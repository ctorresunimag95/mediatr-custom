using ToDoApi.Dispatcher.Contracts;
using ToDoApi.Dispatcher.Wrappers;

namespace ToDoApi.Dispatcher;

internal sealed class Dispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var requestType = command.GetType();

        var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
        var wrapper = (RequestHandlerWrapper<TResponse>)(Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}"))!;

        return await wrapper.Handle(command, _serviceProvider, cancellationToken);
    }

    public async Task SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var requestType = command.GetType();

        var wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
        var wrapper = (RequestHandlerWrapper)(Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}"))!;

        await wrapper.Handle(command, _serviceProvider, cancellationToken);
    }

    public async Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var requestType = query.GetType();
        var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
        var wrapper = (RequestHandlerWrapper<TResponse>)(Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}"))!;
        return await wrapper.Handle(query, _serviceProvider, cancellationToken);
    }
}
