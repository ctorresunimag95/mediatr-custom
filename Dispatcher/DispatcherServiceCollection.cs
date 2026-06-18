using Dispatcher.Decorators;
using Dispatcher.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher;

public class DispatchOptions
{
    public bool UseLogging { get; set; } = true;

    public bool UseValidation { get; set; } = true;
}

public static class DispatcherServiceCollection
{
    public static IServiceCollection AddCustomDispatcher<TAssemblyMarker>(this IServiceCollection services
        , Action<DispatchOptions>? configureOptions = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        => services.AddCustomDispatcher([typeof(TAssemblyMarker)], configureOptions, lifetime);

    public static IServiceCollection AddCustomDispatcher(this IServiceCollection services
        , IEnumerable<Type> assemblyMarkers
        , Action<DispatchOptions>? configureOptions = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var options = new DispatchOptions();
        configureOptions?.Invoke(options);

        var assemblies = assemblyMarkers.Select(t => t.Assembly).Distinct().ToArray();

        RegisterHandlers(typeof(IRequestHandler<,>), services, assemblies, lifetime);
        RegisterHandlers(typeof(IRequestHandler<>), services, assemblies, lifetime);

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

        services.AddTransient<IDispatcher, DispatcherImpl>();

        return services;
    }

    private static void RegisterHandlers(Type handlerType, IServiceCollection services, IEnumerable<System.Reflection.Assembly> assemblies, ServiceLifetime lifetime)
    {
        foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
        {
            if (type is not { IsClass: true, IsAbstract: false })
                continue;

            // Decorators implement IRequestHandler<> too; skip them so they are not
            // registered as handlers (which would make them self-resolve infinitely).
            if (typeof(IDecoratorMarker).IsAssignableFrom(type))
                continue;

            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == handlerType)
                    services.Add(new ServiceDescriptor(i, type, lifetime));
            }
        }
    }

    public static IServiceCollection Decorate(this IServiceCollection services, Type serviceType, Type decoratorType)
    {
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (!IsDecoratorMatch(descriptor.ServiceType, serviceType))
                continue;

            var closedDecorator = decoratorType.IsGenericTypeDefinition
                ? decoratorType.MakeGenericType(descriptor.ServiceType.GetGenericArguments())
                : decoratorType;

            var original = descriptor;
            services[i] = new ServiceDescriptor(
                descriptor.ServiceType,
                sp =>
                {
                    var inner = ResolveInner(sp, original);
                    return ActivatorUtilities.CreateInstance(sp, closedDecorator, inner);
                },
                descriptor.Lifetime);
        }

        return services;
    }

    private static bool IsDecoratorMatch(Type registeredType, Type target)
        => target.IsGenericTypeDefinition
            ? registeredType.IsGenericType && registeredType.GetGenericTypeDefinition() == target
            : registeredType == target;

    private static object ResolveInner(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
            return descriptor.ImplementationInstance;
        if (descriptor.ImplementationFactory is not null)
            return descriptor.ImplementationFactory(sp);
        return ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
    }
}
