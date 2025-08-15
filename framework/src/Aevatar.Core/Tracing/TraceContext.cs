using Orleans;
using Orleans.Runtime;
using System.Threading;
using Aevatar.Core.Abstractions.Tracing;

namespace Aevatar.Core.Tracing;

/// <summary>
/// Manages trace context across both HTTP and Orleans grain call boundaries.
/// Provides unified API for trace context management that works with both
/// AsyncLocal (HTTP contexts) and Orleans RequestContext (grain-to-grain calls).
/// </summary>
public static class TraceContext
{
    private static readonly AsyncLocal<string?> _activeTraceId = new();
    private static readonly AsyncLocal<TraceConfig?> _config = new();
    
    // Orleans RequestContext keys
    private const string TraceIdKey = "AevatarTraceId";
    private const string TraceConfigKey = "AevatarTraceConfig";
    
    /// <summary>
    /// Gets or sets the active trace ID.
    /// Automatically handles both AsyncLocal and Orleans RequestContext.
    /// </summary>
    public static string? ActiveTraceId
    {
        get => GetTraceId();
        set => SetTraceId(value);
    }
    
    /// <summary>
    /// Gets whether tracing is currently enabled for the active context.
    /// </summary>
    public static bool IsTracingEnabled
    {
        get
        {
            var config = GetTraceConfig();
            var traceId = ActiveTraceId;
            
            return config?.ShouldTrace(traceId) == true && config.ShouldSample();
        }
    }
    
    /// <summary>
    /// Sets the trace configuration for the current context.
    /// </summary>
    /// <param name="config">The trace configuration to set.</param>
    public static void SetTraceConfig(TraceConfig config)
    {
        _config.Value = config;
        
        // Also propagate to Orleans context if available
        if (IsOrleansContextAvailable())
        {
            RequestContext.Set(TraceConfigKey, config);
        }
    }
    
    /// <summary>
    /// Gets the current trace configuration.
    /// </summary>
    /// <returns>The current trace configuration, or null if not set.</returns>
    public static TraceConfig? GetTraceConfig()
    {
        // First try AsyncLocal (for HTTP contexts)
        var config = _config.Value;
        if (config != null)
            return config;
        
        // Then try Orleans RequestContext (for grain contexts)
        if (IsOrleansContextAvailable())
        {
            return RequestContext.Get(TraceConfigKey) as TraceConfig;
        }
        
        return null;
    }
    
    /// <summary>
    /// Propagates the current AsyncLocal context to Orleans RequestContext.
    /// Called by outgoing grain call filters.
    /// </summary>
    public static void PropagateToOrleansContext()
    {
        if (!IsOrleansContextAvailable())
            return;
            
        var traceId = _activeTraceId.Value;
        var config = _config.Value;
        
        if (!string.IsNullOrEmpty(traceId))
        {
            RequestContext.Set(TraceIdKey, traceId);
        }
        
        if (config != null)
        {
            RequestContext.Set(TraceConfigKey, config);
        }
    }
    
    /// <summary>
    /// Reads context from Orleans RequestContext and sets AsyncLocal context.
    /// Called by incoming grain call filters.
    /// </summary>
    public static void ReadFromOrleansContext()
    {
        if (!IsOrleansContextAvailable())
            return;
            
        var traceId = RequestContext.Get(TraceIdKey) as string;
        var config = RequestContext.Get(TraceConfigKey) as TraceConfig;
        
        if (!string.IsNullOrEmpty(traceId))
        {
            _activeTraceId.Value = traceId;
        }
        
        if (config != null)
        {
            _config.Value = config;
        }
    }
    
    /// <summary>
    /// Clears the current trace context.
    /// </summary>
    public static void Clear()
    {
        _activeTraceId.Value = null;
        _config.Value = null;
        
        if (IsOrleansContextAvailable())
        {
            RequestContext.Remove(TraceIdKey);
            RequestContext.Remove(TraceConfigKey);
        }
    }

    /// <summary>
    /// Enables tracing for a specific trace ID by setting it as active and adding it to TrackedIds.
    /// Creates a default TraceConfig if none exists.
    /// </summary>
    /// <param name="traceId">The trace ID to enable tracing for.</param>
    /// <param name="enabled">Whether tracing should be enabled globally. Default is true.</param>
    /// <returns>True if tracing was enabled, false if the trace ID is invalid.</returns>
    public static bool EnableTracing(string traceId, bool enabled = true)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            return false;

        // Set the active trace ID
        ActiveTraceId = traceId;

        // Get or create trace config
        var config = GetTraceConfig() ?? new TraceConfig { Enabled = enabled };
        
        // Add the trace ID to tracked IDs
        config.AddTrackedId(traceId);
        
        // Update the config
        SetTraceConfig(config);

        return true;
    }

    /// <summary>
    /// Disables tracing for a specific trace ID by removing it from TrackedIds.
    /// If it's the current active trace ID, clears the active trace ID as well.
    /// </summary>
    /// <param name="traceId">The trace ID to disable tracing for.</param>
    /// <returns>True if tracing was disabled, false if the trace ID wasn't found.</returns>
    public static bool DisableTracing(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            return false;

        var config = GetTraceConfig();
        if (config == null)
            return false;

        var removed = config.RemoveTrackedId(traceId);
        
        // If we removed the currently active trace ID, clear it
        if (removed && ActiveTraceId == traceId)
        {
            ActiveTraceId = null;
        }

        // Update the config
        SetTraceConfig(config);

        return removed;
    }

    /// <summary>
    /// Adds a trace ID to the current TrackedIds without making it the active trace ID.
    /// Creates a default TraceConfig if none exists.
    /// </summary>
    /// <param name="traceId">The trace ID to add to tracking.</param>
    /// <param name="enabled">Whether tracing should be enabled globally. Default is true.</param>
    /// <returns>True if the trace ID was added, false if it already existed or is invalid.</returns>
    public static bool AddTrackedId(string traceId, bool enabled = true)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            return false;

        // Get or create trace config
        var config = GetTraceConfig() ?? new TraceConfig { Enabled = enabled };
        
        // Add the trace ID to tracked IDs
        var added = config.AddTrackedId(traceId);
        
        // Update the config
        SetTraceConfig(config);

        return added;
    }

    /// <summary>
    /// Removes a trace ID from the current TrackedIds.
    /// </summary>
    /// <param name="traceId">The trace ID to remove from tracking.</param>
    /// <returns>True if the trace ID was removed, false if it didn't exist.</returns>
    public static bool RemoveTrackedId(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            return false;

        var config = GetTraceConfig();
        if (config == null)
            return false;

        var removed = config.RemoveTrackedId(traceId);
        
        // Update the config
        SetTraceConfig(config);

        return removed;
    }

    /// <summary>
    /// Gets all currently tracked trace IDs.
    /// </summary>
    /// <returns>A copy of all tracked IDs, or empty set if none.</returns>
    public static HashSet<string> GetTrackedIds()
    {
        var config = GetTraceConfig();
        return config?.GetTrackedIds() ?? new HashSet<string>();
    }
    
    private static string? GetTraceId()
    {
        // First try AsyncLocal (for HTTP contexts)
        var traceId = _activeTraceId.Value;
        if (!string.IsNullOrEmpty(traceId))
            return traceId;
        
        // Then try Orleans RequestContext (for grain contexts)
        if (IsOrleansContextAvailable())
        {
            return RequestContext.Get(TraceIdKey) as string;
        }
        
        return null;
    }
    
    private static void SetTraceId(string? value)
    {
        _activeTraceId.Value = value;
        
        // Also propagate to Orleans context if available
        if (IsOrleansContextAvailable())
        {
            if (!string.IsNullOrEmpty(value))
            {
                RequestContext.Set(TraceIdKey, value);
            }
            else
            {
                RequestContext.Remove(TraceIdKey);
            }
        }
    }
    
    private static bool IsOrleansContextAvailable()
    {
        try
        {
            // This will work if we're in Orleans context
            RequestContext.Get("__test__");
            return true;
        }
        catch
        {
            return false;
        }
    }
} 