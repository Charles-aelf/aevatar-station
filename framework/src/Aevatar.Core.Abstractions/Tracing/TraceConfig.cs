using System.Collections.Generic;

namespace Aevatar.Core.Abstractions.Tracing;

/// <summary>
/// Configuration for method-level tracing.
/// </summary>
[GenerateSerializer]
public class TraceConfig
{
    /// <summary>
    /// Gets or sets whether tracing is globally enabled.
    /// </summary>
    [Id(0)]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the set of IDs that should trigger tracing.
    /// When null or empty, NO requests will be traced (secure by default).
    /// When populated, only requests containing these IDs will be traced.
    /// </summary>
    [Id(1)]
    public HashSet<string>? TrackedIds { get; set; }

    /// <summary>
    /// Gets or sets the sampling rate (0.0 to 1.0).
    /// 1.0 means trace all matching requests.
    /// 0.5 means trace 50% of matching requests.
    /// Default is 1.0.
    /// </summary>
    [Id(2)]
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the maximum number of spans to capture per trace.
    /// Default is 1000 to prevent excessive memory usage.
    /// </summary>
    [Id(3)]
    public int MaxSpansPerTrace { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to capture stack traces on exceptions.
    /// Default is true.
    /// </summary>
    [Id(4)]
    public bool CaptureExceptionStackTrace { get; set; } = true;

    /// <summary>
    /// Determines if the given ID should be traced based on current configuration.
    /// </summary>
    /// <param name="id">The ID to check.</param>
    /// <returns>True if the ID should be traced, false otherwise.</returns>
    public bool ShouldTrace(string? id)
    {
        if (!Enabled)
            return false;

        if (string.IsNullOrEmpty(id))
            return false;

        // If no specific IDs are configured, trace nothing (secure by default)
        if (TrackedIds == null || TrackedIds.Count == 0)
            return false;

        return TrackedIds.Contains(id);
    }

    /// <summary>
    /// Determines if tracing should occur based on sampling rate.
    /// </summary>
    /// <returns>True if should sample, false otherwise.</returns>
    public bool ShouldSample()
    {
        if (SamplingRate >= 1.0)
            return true;

        if (SamplingRate <= 0.0)
            return false;

        return Random.Shared.NextDouble() < SamplingRate;
    }

    /// <summary>
    /// Adds a trace ID to the tracked IDs collection.
    /// Creates the TrackedIds collection if it doesn't exist.
    /// </summary>
    /// <param name="traceId">The trace ID to add.</param>
    /// <returns>True if the ID was added, false if it already existed.</returns>
    public bool AddTrackedId(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            return false;

        TrackedIds ??= new HashSet<string>();
        return TrackedIds.Add(traceId);
    }

    /// <summary>
    /// Removes a trace ID from the tracked IDs collection.
    /// </summary>
    /// <param name="traceId">The trace ID to remove.</param>
    /// <returns>True if the ID was removed, false if it didn't exist.</returns>
    public bool RemoveTrackedId(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId) || TrackedIds == null)
            return false;

        return TrackedIds.Remove(traceId);
    }

    /// <summary>
    /// Clears all tracked IDs. This will disable tracing for all requests.
    /// </summary>
    public void ClearTrackedIds()
    {
        TrackedIds?.Clear();
    }

    /// <summary>
    /// Gets a copy of all currently tracked IDs.
    /// </summary>
    /// <returns>A new HashSet containing all tracked IDs, or empty set if none.</returns>
    public HashSet<string> GetTrackedIds()
    {
        return TrackedIds != null ? new HashSet<string>(TrackedIds) : new HashSet<string>();
    }
} 