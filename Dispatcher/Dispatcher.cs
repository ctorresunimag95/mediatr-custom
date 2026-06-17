using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), RequestHandlerBase> _typedWrapperCache = new();
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapper> _voidWrapperCache = new();

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
        var wrapper = (RequestHandlerWrapper<TResponse>)_typedWrapperCache.GetOrAdd(
            (requestType, typeof(TResponse)),
            static key =>
            {
                var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(key.RequestType, key.ResponseType);
                return (RequestHandlerBase)(Activator.CreateInstance(wrapperType)
                    ?? throw new InvalidOperationException($"Could not create wrapper type for {key.RequestType}"));
            });

        return await wrapper.Handle(request, _serviceProvider, cancellationToken);
    }

    public async Task SendAsync(IRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var wrapper = _voidWrapperCache.GetOrAdd(requestType, static t =>
        {
            var wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(t);
            return (RequestHandlerWrapper)(Activator.CreateInstance(wrapperType)
                ?? throw new InvalidOperationException($"Could not create wrapper type for {t}"));
        });

        await wrapper.Handle(request, _serviceProvider, cancellationToken);
    }
}
