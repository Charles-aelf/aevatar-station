using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Aevatar.Silo.Tracing;
using Aevatar.Core.Tracing;
using Aevatar.Core.Abstractions.Tracing;

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