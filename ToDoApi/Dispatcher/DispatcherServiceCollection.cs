using ToDoApi.Dispatcher.Decorators;
using ToDoApi.Dispatcher.Handlers;

namespace ToDoApi.Dispatcher;

public class DispatchOptions
{
    public bool UseLogging { get; set; } = true;

    public bool UseValidation { get; set; } = true;
}

public static class DispatcherServiceCollection
{
    public static IServiceCollection AddCustomDispatcher(this IServiceCollection services
        , Action<DispatchOptions>? configureOptions = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var options = new DispatchOptions();
        configureOptions?.Invoke(options);

        services.Scan(scan => scan.FromAssembliesOf(typeof(DispatcherServiceCollection))

            .AddClasses(classes => classes
                .AssignableTo(typeof(IRequestHandler<,>))
                .Where(type => !typeof(IDecoratorMarker).IsAssignableFrom(type)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithLifetime(lifetime)

            .AddClasses(classes => classes
                .AssignableTo(typeof(IRequestHandler<>))
                .Where(type => !typeof(IDecoratorMarker).IsAssignableFrom(type)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithLifetime(lifetime)
        );

        if (options.UseValidation)
        {
            services.Decorate(typeof(IRequestHandler<,>), typeof(ValidatorDecorator<,>));
            services.Decorate(typeof(IRequestHandler<>), typeof(ValidatorDecorator<>));
        }

        if (options.UseLogging)
        {
            services.Decorate(typeof(IRequestHandler<,>), typeof(LoggingDecorator<,>));
            services.Decorate(typeof(IRequestHandler<>), typeof(LoggingDecorator<>));
        }

        services.AddTransient<IDispatcher, Dispatcher>();

        return services;
    }
}
