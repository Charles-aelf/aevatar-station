using Xunit;
using FluentAssertions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Aevatar.Core.Tracing;
using Aevatar.Core.Abstractions.Tracing;

namespace Aevatar.Tracing.E2E.Tests;

/// <summary>
/// Tests to verify TraceContext thread safety and isolation in high concurrency scenarios.
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public async Task TraceContext_Should_Isolate_TraceIds_Across_Concurrent_Tasks()
    {
        // Arrange
        const int concurrentTasks = 100;
        var results = new ConcurrentBag<(string ExpectedTraceId, string? ActualTraceId)>();
        
        // Act - Launch many concurrent tasks, each with different trace ID
        var tasks = Enumerable.Range(1, concurrentTasks)
            .Select(async i =>
            {
                var expectedTraceId = $"trace-{i}";
                
                // Set trace ID in this execution context
                TraceContext.ActiveTraceId = expectedTraceId;
                
                // Simulate some async work
                await Task.Delay(Random.Shared.Next(1, 10));
                
                // Verify the trace ID is still correct
                var actualTraceId = TraceContext.ActiveTraceId;
                results.Add((expectedTraceId, actualTraceId));
                
                // More async work
                await Task.Delay(Random.Shared.Next(1, 10));
                
                // Verify again after more async operations
                var secondCheck = TraceContext.ActiveTraceId;
                results.Add((expectedTraceId, secondCheck));
            })
            .ToArray();
        
        await Task.WhenAll(tasks);
        
        // Assert - All tasks should have maintained their isolated trace IDs
        results.Should().HaveCount(concurrentTasks * 2);
        
        foreach (var (expected, actual) in results)
        {
            actual.Should().Be(expected, 
                "Each async execution context should maintain its own trace ID");
        }
    }
    
    [Fact]
    public async Task TraceContext_Should_Not_Interfere_Between_Parallel_HTTP_Simulations()
    {
        // Arrange
        const int concurrentRequests = 50;
        var results = new ConcurrentBag<string>();
        
        // Act - Simulate concurrent HTTP requests
        var httpTasks = Enumerable.Range(1, concurrentRequests)
            .Select(async requestId =>
            {
                // Simulate HTTP middleware setting trace context
                var traceId = $"http-request-{requestId}";
                TraceContext.ActiveTraceId = traceId;
                TraceContext.SetTraceConfig(new TraceConfig 
                { 
                    Enabled = true,
                    TrackedIds = new System.Collections.Generic.HashSet<string> { traceId }
                });
                
                // Simulate business logic with multiple async operations
                await SimulateBusinessLogicAsync();
                
                // Verify trace context is preserved
                var preservedTraceId = TraceContext.ActiveTraceId;
                results.Add(preservedTraceId ?? "null");
            })
            .ToArray();
        
        await Task.WhenAll(httpTasks);
        
        // Assert - Each request should have preserved its trace ID
        results.Should().HaveCount(concurrentRequests);
        
        for (int i = 1; i <= concurrentRequests; i++)
        {
            var expectedTraceId = $"http-request-{i}";
            results.Should().Contain(expectedTraceId,
                $"Request {i} should have preserved its trace ID");
        }
        
        // Verify no duplicate or mixed trace IDs
        results.Distinct().Should().HaveCount(concurrentRequests,
            "Each request should have a unique trace ID with no cross-contamination");
    }
    
    [Fact]
    public async Task TraceContext_Should_Isolate_Config_Across_Concurrent_Operations()
    {
        // Arrange
        const int concurrentOperations = 30;
        var results = new ConcurrentBag<(int OperationId, bool IsEnabled, int TrackedIdCount)>();
        
        // Act - Each operation sets different trace configuration
        var configTasks = Enumerable.Range(1, concurrentOperations)
            .Select(async operationId =>
            {
                // Each operation has different configuration
                var config = new TraceConfig
                {
                    Enabled = operationId % 2 == 0, // Alternating enabled/disabled
                    SamplingRate = operationId * 0.1,
                    TrackedIds = Enumerable.Range(1, operationId % 5 + 1)
                        .Select(x => $"id-{operationId}-{x}")
                        .ToHashSet()
                };
                
                TraceContext.SetTraceConfig(config);
                TraceContext.ActiveTraceId = $"operation-{operationId}";
                
                // Simulate async work
                await Task.Delay(Random.Shared.Next(5, 15));
                
                // Verify configuration is preserved
                var preservedConfig = TraceContext.GetTraceConfig();
                var isEnabled = TraceContext.IsTracingEnabled;
                
                results.Add((operationId, isEnabled, preservedConfig?.TrackedIds?.Count ?? 0));
            })
            .ToArray();
        
        await Task.WhenAll(configTasks);
        
        // Assert - Each operation should have preserved its configuration
        results.Should().HaveCount(concurrentOperations);
        
        foreach (var (operationId, isEnabled, trackedIdCount) in results)
        {
            var expectedEnabled = operationId % 2 == 0;
            var expectedTrackedIdCount = operationId % 5 + 1;
            
            isEnabled.Should().Be(expectedEnabled,
                $"Operation {operationId} should have preserved its enabled state");
            
            trackedIdCount.Should().Be(expectedTrackedIdCount,
                $"Operation {operationId} should have preserved its tracked IDs count");
        }
    }
    
    [Fact]
    public async Task TraceContext_Should_Handle_Task_Run_Context_Boundaries()
    {
        // Arrange
        var mainTraceId = "main-context-trace";
        var taskRunResults = new ConcurrentBag<string>();
        
        // Set trace ID in main context
        TraceContext.ActiveTraceId = mainTraceId;
        
        // Act - Use Task.Run which creates new execution context
        var taskRunOperations = Enumerable.Range(1, 10)
            .Select(async i =>
            {
                await Task.Run(async () =>
                {
                    // Task.Run creates new execution context
                    // AsyncLocal should NOT flow here (this is expected behavior)
                    var taskRunTraceId = TraceContext.ActiveTraceId;
                    
                    // Set new trace ID in Task.Run context
                    TraceContext.ActiveTraceId = $"taskrun-{i}";
                    
                    await Task.Delay(5);
                    
                    var preservedTaskRunTraceId = TraceContext.ActiveTraceId;
                    taskRunResults.Add(preservedTaskRunTraceId ?? "null");
                });
            })
            .ToArray();
        
        await Task.WhenAll(taskRunOperations);
        
        // Assert
        var mainContextTraceId = TraceContext.ActiveTraceId;
        mainContextTraceId.Should().Be(mainTraceId,
            "Main context should preserve its trace ID");
        
        taskRunResults.Should().HaveCount(10);
        
        for (int i = 1; i <= 10; i++)
        {
            var expectedTaskRunTraceId = $"taskrun-{i}";
            taskRunResults.Should().Contain(expectedTaskRunTraceId,
                $"Task.Run operation {i} should have its own isolated trace ID");
        }
    }
    
    [Fact]
    public async Task TraceContext_Should_Handle_Nested_Async_Calls_Correctly()
    {
        // Arrange & Act
        var results = new ConcurrentBag<(int Level, string TraceId)>();
        
        await SimulateNestedAsyncCalls("root-trace", 1, results);
        
        // Assert - All nested calls should see the same trace ID
        results.Should().NotBeEmpty();
        results.Should().OnlyContain(r => r.TraceId == "root-trace",
            "All nested async calls should see the same trace ID from the root context");
        
        // Verify we captured multiple levels
        var levels = results.Select(r => r.Level).Distinct().ToList();
        levels.Should().HaveCountGreaterThan(1,
            "Should have captured multiple nesting levels");
    }
    
    private async Task SimulateNestedAsyncCalls(string traceId, int level, ConcurrentBag<(int Level, string TraceId)> results)
    {
        if (level == 1)
        {
            TraceContext.ActiveTraceId = traceId;
        }
        
        // Capture current trace ID at this level
        var currentTraceId = TraceContext.ActiveTraceId;
        results.Add((level, currentTraceId ?? "null"));
        
        await Task.Delay(1);
        
        if (level < 5)
        {
            // Nested async calls should inherit the trace context
            await SimulateNestedAsyncCalls(traceId, level + 1, results);
        }
    }
    
    private async Task SimulateBusinessLogicAsync()
    {
        // Simulate multiple async operations that might happen in business logic
        await Task.Delay(Random.Shared.Next(1, 5));
        
        // Simulate calling another service
        await SimulateServiceCall();
        
        await Task.Delay(Random.Shared.Next(1, 5));
    }
    
    private async Task SimulateServiceCall()
    {
        await Task.Delay(Random.Shared.Next(1, 3));
        
        // Verify trace context is still available in nested calls
        var traceId = TraceContext.ActiveTraceId;
        traceId.Should().NotBeNull("Trace context should flow through nested async calls");
    }
} 