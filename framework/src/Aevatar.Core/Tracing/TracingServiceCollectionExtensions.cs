using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Castle.DynamicProxy;
using Aevatar.Core.Abstractions.Tracing;

namespace Aevatar.Core.Tracing;

/// <summary>
/// Extensions for adding tracing services to the DI container.
/// </summary>
public static class TracingServiceCollectionExtensions
{
    /// <summary>
    /// Adds tracing services with DynamicProxy support.
    /// </summary>
    public static IServiceCollection AddTracing(this IServiceCollection services, Action<TraceConfig> configure)
    {
        var config = new TraceConfig();
        configure(config);

        // Register tracing components
        services.AddSingleton(config);
        
        // Register Castle DynamicProxy components
        services.AddSingleton<ProxyGenerator>();
        services.AddSingleton<MethodTracingInterceptor>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<MethodTracingInterceptor>>();
            return new MethodTracingInterceptor(logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a scoped service with DynamicProxy interception.
    /// </summary>
    public static IServiceCollection AddProxiedScoped<TService, TImplementation>(
        this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        return services.AddProxiedService<TService, TImplementation>(ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Adds a transient service with DynamicProxy interception.
    /// </summary>
    public static IServiceCollection AddProxiedTransient<TService, TImplementation>(
        this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        return services.AddProxiedService<TService, TImplementation>(ServiceLifetime.Transient);
    }

    /// <summary>
    /// Adds a singleton service with DynamicProxy interception.
    /// </summary>
    public static IServiceCollection AddProxiedSingleton<TService, TImplementation>(
        this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        return services.AddProxiedService<TService, TImplementation>(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Adds a service with DynamicProxy interception based on lifetime.
    /// </summary>
    private static IServiceCollection AddProxiedService<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime)
        where TService : class
        where TImplementation : class, TService
    {
        // Register the concrete implementation
        services.Add(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), lifetime));

        // Register the proxied service
        services.Add(new ServiceDescriptor(typeof(TService), serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<object>>();
            
            // Create proxy with Castle DynamicProxy
            var implementation = serviceProvider.GetRequiredService<TImplementation>();
            var interceptor = serviceProvider.GetRequiredService<MethodTracingInterceptor>();
            var proxyGenerator = serviceProvider.GetRequiredService<ProxyGenerator>();
            
            logger.LogDebug("Creating DynamicProxy for {ServiceType} -> {ImplementationType}", 
                typeof(TService).Name, typeof(TImplementation).Name);
            
            // Create interface proxy targeting the concrete implementation
            var proxy = proxyGenerator.CreateInterfaceProxyWithTarget<TService>(
                implementation,
                interceptor);
            
            logger.LogDebug("Created DynamicProxy for {ServiceType}, Proxy type = {ProxyType}", 
                typeof(TService).Name, proxy.GetType().Name);
            
            return proxy;
        }, lifetime));

        return services;
    }
}
