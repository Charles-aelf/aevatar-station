using Castle.DynamicProxy;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aevatar.Core.Tracing;

/// <summary>
/// Castle DynamicProxy interceptor that provides method-level tracing.
/// Automatically traces methods decorated with TraceAttribute when tracing is enabled.
/// </summary>
public class MethodTracingInterceptor : IInterceptor
{
    private static readonly ActivitySource ActivitySource = new("Aevatar.MethodTracing");

    /// <summary>
    /// Intercepts method calls and applies tracing if conditions are met.
    /// </summary>
    /// <param name="invocation">The method invocation details.</param>
    public void Intercept(IInvocation invocation)
    {
        var traceAttribute = GetTraceAttribute(invocation.Method);
        
        // If method is not marked for tracing, proceed normally
        if (traceAttribute == null)
        {
            invocation.Proceed();
            return;
        }

        // If tracing is not enabled for current context, proceed normally
        if (!TraceContext.IsTracingEnabled)
        {
            invocation.Proceed();
            return;
        }

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
        }
        catch (Exception ex)
        {
            CaptureException(activity, ex);
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
        }
        catch (Exception ex)
        {
            CaptureException(activity, ex);
            throw;
        }
    }

    private Activity? StartActivity(IInvocation invocation, TraceAttribute traceAttribute)
    {
        var activity = ActivitySource.StartActivity(traceAttribute.OperationName);
        
        if (activity == null)
            return null;

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

    private static TraceAttribute? GetTraceAttribute(MethodInfo method)
    {
        return method.GetCustomAttribute<TraceAttribute>();
    }

    private static bool IsAsync(MethodInfo method)
    {
        return typeof(Task).IsAssignableFrom(method.ReturnType);
    }
} 