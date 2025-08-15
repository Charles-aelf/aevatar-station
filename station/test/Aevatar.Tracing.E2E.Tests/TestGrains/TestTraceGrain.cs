using Orleans;
using Aevatar.Core.Tracing;
using System.Diagnostics;

namespace Aevatar.Tracing.E2E.Tests.TestGrains;

/// <summary>
/// Test grain implementation with tracing attributes for E2E testing.
/// </summary>
public class TestTraceGrain : Grain, ITestTraceGrain
{
    /// <summary>
    /// Simple method with trace attribute for testing basic tracing.
    /// </summary>
    [Trace("ProcessMessage", CaptureParameters = true, CaptureReturnValue = true)]
    public async Task<string> ProcessMessageAsync(string message)
    {
        await Task.Delay(10); // Simulate some work
        
        var traceId = TraceContext.ActiveTraceId;
        var result = $"Processed: {message} (TraceId: {traceId}, GrainId: {this.GetPrimaryKeyString()})";
        
        return result;
    }

    /// <summary>
    /// Method that calls another grain to test trace context propagation.
    /// </summary>
    [Trace("CallDownstreamGrain", CaptureParameters = true, CaptureReturnValue = true)]
    public async Task<string> CallDownstreamGrainAsync(string targetGrainId, string message)
    {
        var currentTraceId = TraceContext.ActiveTraceId;
        
        // Call another grain to test context propagation
        var targetGrain = GrainFactory.GetGrain<ITestTraceGrain>(targetGrainId);
        var downstreamResult = await targetGrain.ProcessMessageAsync($"Forwarded: {message}");
        
        return $"Upstream (TraceId: {currentTraceId}) -> {downstreamResult}";
    }

    /// <summary>
    /// Method that throws an exception to test exception tracing.
    /// </summary>
    [Trace("ThrowException", CaptureParameters = true)]
    public Task<string> ThrowExceptionAsync(string errorMessage)
    {
        var traceId = TraceContext.ActiveTraceId;
        throw new InvalidOperationException($"Test exception: {errorMessage} (TraceId: {traceId})");
    }

    /// <summary>
    /// Method with complex parameters to test parameter capture.
    /// </summary>
    [Trace("ProcessComplexData", CaptureParameters = true, CaptureReturnValue = true, MaxCaptureSize = 2048)]
    public async Task<TestResult> ProcessComplexDataAsync(TestData complexData)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Simulate complex processing
        await Task.Delay(50);
        
        var traceId = TraceContext.ActiveTraceId;
        var processedId = $"{complexData.Id}-processed-{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        stopwatch.Stop();
        
        return new TestResult(
            processedId,
            $"Processed {complexData.Name} with value {complexData.Value} (TraceId: {traceId})",
            true,
            stopwatch.Elapsed
        );
    }
} 