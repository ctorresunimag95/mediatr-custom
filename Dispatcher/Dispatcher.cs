using Dispatcher.Contracts;
using Dispatcher.Wrappers;

namespace Dispatcher;

public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);

    Task SendAsync(ICommand command, CancellationToken cancellationToken);

    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);

    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);

    Task SendAsync(IRequest request, CancellationToken cancellationToken);
}

internal sealed class DispatcherImpl : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public DispatcherImpl(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
        => SendAsync((IRequest<TResponse>)command, cancellationToken);

    public Task SendAsync(ICommand command, CancellationToken cancellationToken)
        => SendAsync((IRequest)command, cancellationToken);

    public Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
        => SendAsync((IRequest<TResponse>)query, cancellationToken);

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
        var wrapper = (RequestHandlerWrapper<TResponse>)(Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}"))!;

        return await wrapper.Handle(request, _serviceProvider, cancellationToken);
    }

    public async Task SendAsync(IRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
        var wrapper = (RequestHandlerWrapper)(Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}"))!;

        await wrapper.Handle(request, _serviceProvider, cancellationToken);
    }
}
