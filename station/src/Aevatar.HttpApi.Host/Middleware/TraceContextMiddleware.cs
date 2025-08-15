using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Aevatar.Core.Tracing;
using Aevatar.Core.Abstractions.Tracing;

namespace Aevatar.HttpApi.Host.Middleware;

/// <summary>
/// Middleware that extracts trace IDs from HTTP requests and sets the trace context
/// for downstream processing including Orleans grain calls.
/// </summary>
public class TraceContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TraceConfig _defaultConfig;

    /// <summary>
    /// Initializes a new instance of the TraceContextMiddleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="defaultConfig">Default trace configuration. If null, a default configuration will be used.</param>
    public TraceContextMiddleware(RequestDelegate next, TraceConfig? defaultConfig = null)
    {
        _next = next;
        _defaultConfig = defaultConfig ?? new TraceConfig { Enabled = true };
    }

    /// <summary>
    /// Invokes the middleware, extracting trace context from the HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Extract trace ID from various sources in order of preference
        var traceId = ExtractTraceId(context);
        
        if (!string.IsNullOrEmpty(traceId))
        {
            // Set trace context for this request
            TraceContext.ActiveTraceId = traceId;
            TraceContext.SetTraceConfig(_defaultConfig);
            
            // Optionally add trace ID to response headers for debugging
            if (_defaultConfig.Enabled)
            {
                context.Response.Headers.Add("X-Trace-Id", traceId);
            }
        }

        try
        {
            await _next(context);
        }
        finally
        {
            // Clear trace context after request completes
            TraceContext.Clear();
        }
    }

    /// <summary>
    /// Extracts trace ID from HTTP request in order of preference:
    /// 1. Query parameter "traceId" 
    /// 2. Header "X-Trace-Id"
    /// 3. Header "TraceId"
    /// 4. Route value "traceId"
    /// 5. Custom extraction from request path
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The extracted trace ID, or null if not found.</returns>
    private string? ExtractTraceId(HttpContext context)
    {
        // 1. Check query parameter
        if (context.Request.Query.TryGetValue("traceId", out var queryTraceId) && 
            !string.IsNullOrEmpty(queryTraceId))
        {
            return queryTraceId.ToString();
        }

        // 2. Check X-Trace-Id header
        if (context.Request.Headers.TryGetValue("X-Trace-Id", out var headerTraceId) && 
            !string.IsNullOrEmpty(headerTraceId))
        {
            return headerTraceId.ToString();
        }

        // 3. Check TraceId header
        if (context.Request.Headers.TryGetValue("TraceId", out var traceIdHeader) && 
            !string.IsNullOrEmpty(traceIdHeader))
        {
            return traceIdHeader.ToString();
        }

        // 4. Check route values
        if (context.Request.RouteValues.TryGetValue("traceId", out var routeTraceId) && 
            routeTraceId != null)
        {
            return routeTraceId.ToString();
        }

        // 5. Custom extraction from common ID parameters in route
        // Look for common entity IDs that might be used for tracing
        foreach (var routeValue in context.Request.RouteValues)
        {
            var key = routeValue.Key.ToLowerInvariant();
            var value = routeValue.Value?.ToString();
            
            if (!string.IsNullOrEmpty(value) && 
                (key.Contains("id") || key.Contains("userid") || key.Contains("customerid") || 
                 key.Contains("orderid") || key.Contains("sessionid")))
            {
                return value;
            }
        }

        // 6. Extract from query parameters that end with "Id"
        foreach (var queryParam in context.Request.Query)
        {
            var key = queryParam.Key.ToLowerInvariant();
            var value = queryParam.Value.ToString();
            
            if (!string.IsNullOrEmpty(value) && key.EndsWith("id"))
            {
                return value;
            }
        }

        return null;
    }
} 