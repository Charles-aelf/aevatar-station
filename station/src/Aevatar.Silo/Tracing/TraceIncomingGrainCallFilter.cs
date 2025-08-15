using Orleans;
using Orleans.Runtime;
using System.Threading.Tasks;
using Aevatar.Core.Tracing;

namespace Aevatar.Silo.Tracing;

/// <summary>
/// Incoming grain call filter that reads trace context from Orleans RequestContext
/// and sets the local AsyncLocal context for method tracing.
/// </summary>
public class TraceIncomingGrainCallFilter : IIncomingGrainCallFilter
{
    /// <summary>
    /// Invokes the filter, reading trace context from Orleans RequestContext.
    /// </summary>
    /// <param name="context">The incoming grain call context.</param>
    /// <returns>A task representing the filter work.</returns>
    public async Task Invoke(IIncomingGrainCallContext context)
    {
        // Read context from Orleans RequestContext and set local AsyncLocal
        TraceContext.ReadFromOrleansContext();
        
        try
        {
            // Orleans handles returning the result to the caller automatically
            // The filter just needs to call context.Invoke() and let it pass through
            await context.Invoke();
        }
        finally
        {
            // Optional: Clear context after grain call completes
            // Note: This might interfere with outgoing calls from this grain
            // TraceContext.Clear();
        }
    }
} 