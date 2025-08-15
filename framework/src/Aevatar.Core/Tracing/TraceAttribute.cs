using System;

namespace Aevatar.Core.Tracing;

/// <summary>
/// Attribute to mark methods for comprehensive tracing.
/// Methods decorated with this attribute will be automatically traced when
/// the trace context contains a matching ID.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TraceAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the operation for tracing purposes.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets or sets whether to capture method parameters in traces.
    /// Default is false for performance and privacy reasons.
    /// </summary>
    public bool CaptureParameters { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to capture return values in traces.
    /// Default is false for performance and privacy reasons.
    /// </summary>
    public bool CaptureReturnValue { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to capture local variables in traces.
    /// Default is false for performance and privacy reasons.
    /// Note: This feature may require additional instrumentation.
    /// </summary>
    public bool CaptureLocalVariables { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum size of captured data in bytes.
    /// Default is 1024 bytes to prevent excessive memory usage.
    /// </summary>
    public int MaxCaptureSize { get; set; } = 1024;

    /// <summary>
    /// Initializes a new instance of the TraceAttribute class.
    /// </summary>
    /// <param name="operationName">The name of the operation for tracing purposes.</param>
    public TraceAttribute(string operationName)
    {
        OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
    }
} 