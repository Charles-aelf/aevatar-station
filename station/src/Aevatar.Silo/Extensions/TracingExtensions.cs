using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Aevatar.Silo.Tracing;
using Aevatar.Core.Tracing;
using Aevatar.Core.Abstractions.Tracing;
using Castle.DynamicProxy;
using Aevatar.Core.Tracing; // This makes the extension methods available

namespace Aevatar.Silo.Extensions;

/// <summary>
/// Extensions for configuring method-level tracing in Orleans silo.
/// </summary>
public static class TracingExtensions
{
    /// <summary>
    /// Adds method-level tracing to the Orleans silo.
    /// This configures grain call filters to propagate trace context across grain boundaries.
    /// </summary>
    /// <param name="builder">The silo builder.</param>
    /// <returns>The silo builder for method chaining.</returns>
    public static ISiloBuilder AddMethodTracing(this ISiloBuilder builder)
    {
        return builder
            .AddIncomingGrainCallFilter<TraceIncomingGrainCallFilter>()
            .AddOutgoingGrainCallFilter<TraceOutgoingGrainCallFilter>()
            .ConfigureServices(services =>
            {
                // Register tracing components
                services.AddSingleton<MethodTracingInterceptor>();
                
                // Register grain call filters as singletons for performance
                services.AddSingleton<TraceIncomingGrainCallFilter>();
                services.AddSingleton<TraceOutgoingGrainCallFilter>();
                
                // Register Castle DynamicProxy components for service interception
                services.AddSingleton<Castle.DynamicProxy.ProxyGenerator>();
                
                // Add framework tracing services
                services.AddTracing(cfg =>
                {
                    cfg.Enabled = true;
                    cfg.SamplingRate = 1.0;
                    cfg.TrackedIds = new HashSet<string> { "orleans-trace" };
                });
                
                // Register the DynamicProxy service registration methods
                services.AddSingleton<Castle.DynamicProxy.ProxyGenerator>();
                
                // Now the services.AddProxiedScoped<TService, TImplementation>() methods will work
            });
    }

    /// <summary>
    /// Adds method-level tracing with a custom trace configuration.
    /// </summary>
    /// <param name="builder">The silo builder.</param>
    /// <param name="configure">Action to configure the trace settings.</param>
    /// <returns>The silo builder for method chaining.</returns>
    public static ISiloBuilder AddMethodTracing(this ISiloBuilder builder, Action<TraceConfig> configure)
    {
        var config = new TraceConfig();
        configure(config);

        return builder
            .AddMethodTracing()
            .ConfigureServices(services =>
            {
                services.AddSingleton(config);
            });
    }
} 