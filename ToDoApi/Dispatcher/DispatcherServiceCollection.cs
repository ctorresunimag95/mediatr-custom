using ToDoApi.Dispatcher.Decorators;
using ToDoApi.Dispatcher.Handlers;

namespace ToDoApi.Dispatcher;

public static class DispatcherServiceCollection
{
    public static IServiceCollection AddCustomDispatcher(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        services.Scan(scan => scan.FromAssembliesOf(typeof(DispatcherServiceCollection))

            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithLifetime(lifetime)

            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithLifetime(lifetime)
        );

        services.Decorate(typeof(IRequestHandler<,>), typeof(LoggingDecorator<,>));
        services.Decorate(typeof(IRequestHandler<>), typeof(LoggingDecorator<>));

        services.AddTransient<Dispatcher>();

        return services;
    }
}
