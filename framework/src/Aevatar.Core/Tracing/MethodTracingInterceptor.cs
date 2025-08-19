using Castle.DynamicProxy;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Tracing;

    /// <summary>
    /// Castle DynamicProxy interceptor that provides method-level tracing.
    /// Automatically traces methods decorated with TraceAttribute when tracing is enabled.
    /// </summary>
    public class MethodTracingInterceptor : IInterceptor
    {
        private static readonly ActivitySource ActivitySource = new("Aevatar.MethodTracing");
        private readonly ILogger<MethodTracingInterceptor> _logger;

        public MethodTracingInterceptor(ILogger<MethodTracingInterceptor> logger)
        {
            _logger = logger;
        }

    /// <summary>
    /// Intercepts method calls and applies tracing if conditions are met.
    /// </summary>
    /// <param name="invocation">The method invocation details.</param>
    public void Intercept(IInvocation invocation)
    {
        _logger.LogDebug("[INTERCEPT] Method called: {TargetType}.{MethodName}", 
            invocation.TargetType?.Name, invocation.Method.Name);
        
        var traceAttribute = GetTraceAttribute(invocation.Method);
        
        if (traceAttribute != null)
        {
            _logger.LogDebug("[INTERCEPT] Found TraceAttribute: {OperationName}", 
                traceAttribute.OperationName ?? invocation.Method.Name);
        }
        else
        {
            _logger.LogDebug("[INTERCEPT] No TraceAttribute found");
        }
        
        // If method is not marked for tracing, proceed normally
        if (traceAttribute == null)
        {
            _logger.LogDebug("[INTERCEPT] Proceeding without tracing");
            invocation.Proceed();
            return;
        }

        // If tracing is not enabled for current context, proceed normally
        if (!TraceContext.IsTracingEnabled)
        {
            _logger.LogDebug("[INTERCEPT] Tracing disabled, proceeding without tracing");
            invocation.Proceed();
            return;
        }

        _logger.LogDebug("[INTERCEPT] Starting traced execution for {OperationName}", 
            traceAttribute.OperationName ?? invocation.Method.Name);

        // Perform traced execution
        if (IsAsync(invocation.Method))
        {
            InterceptAsync(invocation, traceAttribute);
        }
        else
        {
            InterceptSync(invocation, traceAttribute);
        }
    }

    private void InterceptSync(IInvocation invocation, TraceAttribute traceAttribute)
    {
        using var activity = StartActivity(invocation, traceAttribute);
        
        try
        {
            invocation.Proceed();
            
            // Capture return value if configured
            if (traceAttribute.CaptureReturnValue && invocation.ReturnValue != null)
            {
                CaptureReturnValue(activity, invocation.ReturnValue, traceAttribute.MaxCaptureSize);
            }
            
            // Log when activity completes successfully
            if (activity != null)
            {
                _logger.LogInformation("[TRACING] Completed OpenTelemetry activity: {OperationName} for {TargetType}.{MethodName}", 
                    activity.OperationName, invocation.TargetType?.Name, invocation.Method.Name);
            }
        }
        catch (Exception ex)
        {
            CaptureException(activity, ex);
            if (activity != null)
            {
                _logger.LogError(ex, "[TRACING] Failed OpenTelemetry activity: {OperationName} for {TargetType}.{MethodName}", 
                    activity.OperationName, invocation.TargetType?.Name, invocation.Method.Name);
            }
            throw;
        }
    }

    private void InterceptAsync(IInvocation invocation, TraceAttribute traceAttribute)
    {
        invocation.Proceed();
        
        if (invocation.ReturnValue is Task task)
        {
            invocation.ReturnValue = InterceptAsyncTask(task, invocation, traceAttribute);
        }
    }

    private async Task InterceptAsyncTask(Task originalTask, IInvocation invocation, TraceAttribute traceAttribute)
    {
        using var activity = StartActivity(invocation, traceAttribute);
        
        try
        {
            await originalTask;
            
            // For Task<T>, capture return value if configured
            if (traceAttribute.CaptureReturnValue && originalTask.GetType().IsGenericType)
            {
                var resultProperty = originalTask.GetType().GetProperty("Result");
                if (resultProperty != null)
                {
                    var result = resultProperty.GetValue(originalTask);
                    if (result != null)
                    {
                        CaptureReturnValue(activity, result, traceAttribute.MaxCaptureSize);
                    }
                }
            }
            
            // Log when activity completes successfully
            if (activity != null)
            {
                _logger.LogInformation("[TRACING] Completed OpenTelemetry activity: {OperationName} for {TargetType}.{MethodName}", 
                    activity.OperationName, invocation.TargetType?.Name, invocation.Method.Name);
            }
        }
        catch (Exception ex)
        {
            CaptureException(activity, ex);
            if (activity != null)
            {
                _logger.LogError(ex, "[TRACING] Failed OpenTelemetry activity: {OperationName} for {TargetType}.{MethodName}", 
                    activity.OperationName, invocation.TargetType?.Name, invocation.Method.Name);
            }
            throw;
        }
    }

    private Activity? StartActivity(IInvocation invocation, TraceAttribute traceAttribute)
    {
        var activity = ActivitySource.StartActivity(traceAttribute.OperationName);
        
        if (activity == null)
            return null;

        _logger.LogDebug("[TRACING] Created OpenTelemetry activity: {OperationName} for {TargetType}.{MethodName}", 
            activity.OperationName, invocation.TargetType?.Name, invocation.Method.Name);

        // Add basic tags
        activity.SetTag("method.class", invocation.TargetType?.Name ?? "Unknown");
        activity.SetTag("method.name", invocation.Method.Name);
        activity.SetTag("trace.id", TraceContext.ActiveTraceId);
        
        // Capture parameters if configured
        if (traceAttribute.CaptureParameters)
        {
            CaptureParameters(activity, invocation, traceAttribute.MaxCaptureSize);
        }

        return activity;
    }

    private void CaptureParameters(Activity? activity, IInvocation invocation, int maxSize)
    {
        if (activity == null) return;

        try
        {
            var parameters = invocation.Method.GetParameters();
            for (int i = 0; i < parameters.Length && i < invocation.Arguments.Length; i++)
            {
                var param = parameters[i];
                var value = invocation.Arguments[i];
                
                if (value != null)
                {
                    var serialized = SerializeValue(value, maxSize);
                    activity.SetTag($"parameter.{param.Name}", serialized);
                }
                else
                {
                    activity.SetTag($"parameter.{param.Name}", "null");
                }
            }
        }
        catch (Exception ex)
        {
            activity.SetTag("parameter.capture.error", ex.Message);
        }
    }

    private void CaptureReturnValue(Activity? activity, object value, int maxSize)
    {
        if (activity == null) return;

        try
        {
            var serialized = SerializeValue(value, maxSize);
            activity.SetTag("return.value", serialized);
        }
        catch (Exception ex)
        {
            activity.SetTag("return.capture.error", ex.Message);
        }
    }

    private void CaptureException(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        
        var config = TraceContext.GetTraceConfig();
        if (config?.CaptureExceptionStackTrace == true)
        {
            activity.SetTag("exception.stacktrace", exception.StackTrace);
        }
    }

    private string SerializeValue(object value, int maxSize)
    {
        try
        {
            // For simple types, use ToString()
            if (value.GetType().IsPrimitive || value is string || value is DateTime || value is Guid)
            {
                var str = value.ToString() ?? "";
                return str.Length > maxSize ? str.Substring(0, maxSize) + "..." : str;
            }

            // For complex types, use JSON serialization
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 3 // Prevent deep object graphs
            });

            return json.Length > maxSize ? json.Substring(0, maxSize) + "..." : json;
        }
        catch
        {
            return $"[{value.GetType().Name}]";
        }
    }

    private TraceAttribute? GetTraceAttribute(MethodInfo method)
    {
        _logger.LogDebug("[ATTR] Checking for TraceAttribute on method {MethodName}", method.Name);
        _logger.LogDebug("[ATTR] Method declaring type: {DeclaringType}", method.DeclaringType?.Name ?? "Unknown");
        _logger.LogDebug("[ATTR] Method type: {MethodType}", method.GetType().Name);
        
        // Log ALL attributes on the method to see what we actually have
        var allAttributes = method.GetCustomAttributes();
        _logger.LogDebug("[ATTR] All attributes on method {MethodName}: {AttributeCount}", method.Name, allAttributes.Count());
        foreach (var attr in allAttributes)
        {
            _logger.LogDebug("[ATTR] Found attribute: {AttributeType} on {MethodName}", attr.GetType().Name, method.Name);
        }
        
        // Direct attribute lookup on the method
        var methodAttribute = method.GetCustomAttribute<TraceAttribute>();
        if (methodAttribute != null)
        {
            _logger.LogDebug("[ATTR] Found TraceAttribute on method {MethodName}: {OperationName}", 
                method.Name, methodAttribute.OperationName ?? method.Name);
            return methodAttribute;
        }

        // Check class-level attribute if method-level not found
        var classAttribute = method.DeclaringType?.GetCustomAttribute<TraceAttribute>();
        if (classAttribute != null)
        {
            _logger.LogDebug("[ATTR] Found TraceAttribute on class {ClassType}: {OperationName}", 
                method.DeclaringType?.Name, classAttribute.OperationName ?? method.Name);
            return classAttribute;
        }
        
        _logger.LogDebug("[ATTR] No TraceAttribute found for method {MethodName}", method.Name);
        return null;
    }

    private static bool IsAsync(MethodInfo method)
    {
        return typeof(Task).IsAssignableFrom(method.ReturnType);
    }
} 