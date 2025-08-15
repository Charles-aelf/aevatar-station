using System;
using Microsoft.AspNetCore.Builder;
using Aevatar.HttpApi.Host.Middleware;
using Aevatar.Core.Abstractions.Tracing;

namespace Aevatar.HttpApi.Host.Extensions;

/// <summary>
/// Extensions for configuring HTTP trace context middleware.
/// </summary>
public static class TracingExtensions
{
    /// <summary>
    /// Adds trace context middleware to the HTTP pipeline.
    /// This middleware extracts trace IDs from requests and sets trace context for downstream processing.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder UseTraceContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TraceContextMiddleware>();
    }

    /// <summary>
    /// Adds trace context middleware with custom configuration.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Action to configure the trace settings.</param>
    /// <returns>The application builder for method chaining.</returns>
    public static IApplicationBuilder UseTraceContext(this IApplicationBuilder app, Action<TraceConfig> configure)
    {
        var config = new TraceConfig();
        configure(config);
        
        return app.UseMiddleware<TraceContextMiddleware>(config);
    }
} 