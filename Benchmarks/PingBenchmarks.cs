using BenchmarkDotNet.Attributes;
using Dispatcher;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

[MemoryDiagnoser]
public class PingBenchmarks
{
    private IServiceScope _customPureScope = null!;
    private IServiceScope _customDecoratedScope = null!;
    private IServiceScope _mediatrPureScope = null!;
    private IServiceScope _mediatrDecoratedScope = null!;

    private IDispatcher _customPure = null!;
    private IDispatcher _customDecorated = null!;
    private IMediator _mediatrPure = null!;
    private IMediator _mediatrDecorated = null!;

    private readonly CustomPingRequest _customRequest = new(42);
    private readonly MediatrPingRequest _mediatrRequest = new(42);

    [GlobalSetup]
    public void Setup()
    {
        _customPureScope = BuildCustomProvider(useDecorators: false).CreateScope();
        _customPure = _customPureScope.ServiceProvider.GetRequiredService<IDispatcher>();

        _customDecoratedScope = BuildCustomProvider(useDecorators: true).CreateScope();
        _customDecorated = _customDecoratedScope.ServiceProvider.GetRequiredService<IDispatcher>();

        _mediatrPureScope = BuildMediatrProvider(useBehaviors: false).CreateScope();
        _mediatrPure = _mediatrPureScope.ServiceProvider.GetRequiredService<IMediator>();

        _mediatrDecoratedScope = BuildMediatrProvider(useBehaviors: true).CreateScope();
        _mediatrDecorated = _mediatrDecoratedScope.ServiceProvider.GetRequiredService<IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _customPureScope.Dispose();
        _customDecoratedScope.Dispose();
        _mediatrPureScope.Dispose();
        _mediatrDecoratedScope.Dispose();
    }

    private static IServiceProvider BuildCustomProvider(bool useDecorators)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IValidator<CustomPingRequest>, CustomPingRequestValidator>();
        services.AddCustomDispatcher<PingBenchmarks>(o =>
        {
            o.UseLogging = useDecorators;
            o.UseValidation = useDecorators;
        });
        return services.BuildServiceProvider();
    }

    private static IServiceProvider BuildMediatrProvider(bool useBehaviors)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IValidator<MediatrPingRequest>, MediatrPingRequestValidator>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(MediatrPingRequest).Assembly);
            if (useBehaviors)
            {
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            }
        });
        return services.BuildServiceProvider();
    }

    [Benchmark(Baseline = true)]
    public Task<Pong> CustomDispatcher_Pure()
        => _customPure.SendAsync(_customRequest, CancellationToken.None);

    [Benchmark]
    public Task<Pong> MediatR_Pure()
        => _mediatrPure.Send(_mediatrRequest, CancellationToken.None);

    [Benchmark]
    public Task<Pong> CustomDispatcher_Decorated()
        => _customDecorated.SendAsync(_customRequest, CancellationToken.None);

    [Benchmark]
    public Task<Pong> MediatR_Decorated()
        => _mediatrDecorated.Send(_mediatrRequest, CancellationToken.None);
}
