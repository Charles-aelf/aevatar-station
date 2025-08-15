using System.Threading;
using System.Threading.Tasks;

namespace Aevatar.Core.Abstractions.Tracing;

/// <summary>
/// Service for managing trace configuration at runtime.
/// Allows dynamic enabling/disabling of tracing and updating tracked IDs.
/// </summary>
public interface ITraceConfigService
{
    /// <summary>
    /// Gets the current trace configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current trace configuration.</returns>
    Task<TraceConfig> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the trace configuration.
    /// </summary>
    /// <param name="config">The new configuration to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateConfigAsync(TraceConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables tracing for the specified ID.
    /// </summary>
    /// <param name="id">The ID to enable tracing for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnableTracingForIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables tracing for the specified ID.
    /// </summary>
    /// <param name="id">The ID to disable tracing for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisableTracingForIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables tracing globally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnableTracingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables tracing globally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisableTracingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the sampling rate for tracing.
    /// </summary>
    /// <param name="samplingRate">The new sampling rate (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSamplingRateAsync(double samplingRate, CancellationToken cancellationToken = default);
} 