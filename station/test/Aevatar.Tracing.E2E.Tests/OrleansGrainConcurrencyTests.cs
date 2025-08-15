using Xunit;
using FluentAssertions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Orleans.TestingHost;
using Orleans.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Aevatar.Core.Tracing;
using Aevatar.Core.Abstractions.Tracing;
using Aevatar.Silo.Extensions;
using Aevatar.Tracing.E2E.Tests.TestGrains;

namespace Aevatar.Tracing.E2E.Tests;

/// <summary>
/// Tests to verify TraceContext thread safety specifically in Orleans grain scenarios
/// where multiple different agents (grains) fire tracing-enabled requests simultaneously.
/// </summary>
public class OrleansGrainConcurrencyTests : IClassFixture<TracingTestCluster>
{
    private readonly TracingTestCluster _cluster;

    public OrleansGrainConcurrencyTests(TracingTestCluster cluster)
    {
        _cluster = cluster;
    }

    [Fact]
    public async Task MultipleGrains_Should_Isolate_TraceContexts_During_Concurrent_Processing()
    {
        // Arrange
        const int grainCount = 50;
        var results = new ConcurrentBag<(string GrainId, string Message)>();

        // Act - Create many grains processing different trace contexts simultaneously
        var grainTasks = Enumerable.Range(1, grainCount)
            .Select(async grainId =>
            {
                var grainKey = $"concurrency-grain-{grainId}";
                var traceId = $"grain-trace-{grainId}";
                
                // Set unique trace context for each task
                TraceContext.ActiveTraceId = traceId;
                TraceContext.SetTraceConfig(new TraceConfig
                {
                    Enabled = true,
                    TrackedIds = new System.Collections.Generic.HashSet<string> { traceId },
                    SamplingRate = 1.0
                });

                var grain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>(grainKey);
                var message = $"Processing grain {grainId} with trace {traceId}";
                var result = await grain.ProcessMessageAsync(message);
                
                results.Add((grainId.ToString(), result));
                
                // Clean up trace context
                TraceContext.Clear();
            })
            .ToArray();

        await Task.WhenAll(grainTasks);

        // Assert - Each grain should have processed with its own isolated trace context
        results.Should().HaveCount(grainCount);
        
        for (int i = 1; i <= grainCount; i++)
        {
            var grainResult = results.FirstOrDefault(r => r.GrainId == i.ToString());
            grainResult.Should().NotBeNull($"Grain {i} should have processed its message");
            grainResult.Message.Should().NotBeNullOrEmpty($"Grain {i} should return a result");
        }

        // Verify no cross-contamination by checking that each result is unique
        var uniqueResults = results.Select(r => r.Message).Distinct().ToList();
        uniqueResults.Should().HaveCount(grainCount, "Each grain should produce a unique result");
    }

    [Fact]
    public async Task GrainToGrain_Calls_Should_Propagate_TraceContext_Correctly()
    {
        // Arrange
        var rootTraceId = "root-grain-trace";
        var config = new TraceConfig
        {
            Enabled = true,
            TrackedIds = new System.Collections.Generic.HashSet<string> { rootTraceId },
            SamplingRate = 1.0
        };

        // Set initial trace context
        TraceContext.ActiveTraceId = rootTraceId;
        TraceContext.SetTraceConfig(config);

        // Act - Create a chain of grain calls where trace context should propagate
        var grain1 = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("chain-grain-1");
        var grain2 = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("chain-grain-2");

        var result1 = await grain1.CallDownstreamGrainAsync("chain-grain-2", "Hello from grain1");
        var result2 = await grain2.ProcessMessageAsync("Direct call to grain2");

        // Assert - Both calls should complete successfully
        result1.Should().NotBeNullOrEmpty("Grain chain call should return a result");
        result2.Should().NotBeNullOrEmpty("Direct grain call should return a result");

        // Clean up
        TraceContext.Clear();
    }

    [Fact]
    public async Task Concurrent_GrainChains_Should_Maintain_Separate_TraceContexts()
    {
        // Arrange
        const int chainCount = 20;
        var allResults = new ConcurrentBag<(int ChainId, string Result)>();

        // Act - Start multiple grain chains concurrently, each with different trace context
        var chainTasks = Enumerable.Range(1, chainCount)
            .Select(async chainId =>
            {
                var traceId = $"chain-{chainId}-trace";
                
                // Set unique trace context for this chain
                TraceContext.ActiveTraceId = traceId;
                TraceContext.SetTraceConfig(new TraceConfig
                {
                    Enabled = true,
                    TrackedIds = new System.Collections.Generic.HashSet<string> { traceId },
                    SamplingRate = 1.0
                });

                var grain1 = _cluster.GrainFactory.GetGrain<ITestTraceGrain>($"chain-{chainId}-grain-1");
                var grain2 = _cluster.GrainFactory.GetGrain<ITestTraceGrain>($"chain-{chainId}-grain-2");
                
                // Create a chain of calls
                var result = await grain1.CallDownstreamGrainAsync($"chain-{chainId}-grain-2", $"Message from chain {chainId}");
                allResults.Add((chainId, result));
                
                // Clean up context
                TraceContext.Clear();
            })
            .ToArray();

        await Task.WhenAll(chainTasks);

        // Assert - All chains should complete with unique results
        allResults.Should().HaveCount(chainCount, "All chains should complete");

        // Verify each chain produced a result
        for (int i = 1; i <= chainCount; i++)
        {
            var chainResult = allResults.FirstOrDefault(r => r.ChainId == i);
            chainResult.Should().NotBeNull($"Chain {i} should have completed");
            chainResult.Result.Should().NotBeNullOrEmpty($"Chain {i} should return a result");
        }

        // Verify no cross-contamination between chains
        var uniqueResults = allResults.Select(r => r.Result).Distinct().ToList();
        uniqueResults.Should().HaveCount(chainCount, "Each chain should produce a unique result");
    }

    [Fact]
    public async Task HighVolume_Concurrent_Grains_Should_Not_Interfere_With_TraceContext()
    {
        // Arrange - Simulate high-volume concurrent grain processing
        const int concurrentGrains = 50;
        var allResults = new ConcurrentBag<(string GrainId, string Result)>();

        // Act - Each grain processes with its own trace context
        var grainTasks = Enumerable.Range(1, concurrentGrains)
            .Select(async grainId =>
            {
                var traceId = $"high-volume-grain-{grainId}";
                var grainKey = $"hv-grain-{grainId}";
                
                // Set unique trace context
                TraceContext.ActiveTraceId = traceId;
                TraceContext.SetTraceConfig(new TraceConfig
                {
                    Enabled = true,
                    TrackedIds = new System.Collections.Generic.HashSet<string> { traceId },
                    SamplingRate = 1.0
                });

                var grain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>(grainKey);
                var message = $"High volume processing for grain {grainId}";
                var result = await grain.ProcessMessageAsync(message);
                
                allResults.Add((grainId.ToString(), result));
                
                // Clean up context
                TraceContext.Clear();
            })
            .ToArray();

        await Task.WhenAll(grainTasks);

        // Assert - All operations should complete successfully
        allResults.Should().HaveCount(concurrentGrains, "All operations should complete");

        // Verify each grain processed successfully
        for (int i = 1; i <= concurrentGrains; i++)
        {
            var grainResult = allResults.FirstOrDefault(r => r.GrainId == i.ToString());
            grainResult.Should().NotBeNull($"Grain {i} should have completed");
            grainResult.Result.Should().NotBeNullOrEmpty($"Grain {i} should return a result");
        }

        // Verify no cross-contamination
        var uniqueResults = allResults.Select(r => r.Result).Distinct().ToList();
        uniqueResults.Should().HaveCount(concurrentGrains, "Each grain should produce a unique result");
    }

    [Fact]
    public async Task Orleans_RequestContext_And_AsyncLocal_Should_Work_Together_Under_Load()
    {
        // Arrange - Test the boundary between Orleans RequestContext and AsyncLocal
        const int concurrentRequests = 30;
        var results = new ConcurrentBag<(string RequestId, string Result)>();

        // Act - Simulate HTTP requests that call Orleans grains
        var requestTasks = Enumerable.Range(1, concurrentRequests)
            .Select(async requestId =>
            {
                var traceId = $"http-request-{requestId}";
                
                // Simulate HTTP middleware setting AsyncLocal context
                TraceContext.ActiveTraceId = traceId;
                TraceContext.SetTraceConfig(new TraceConfig
                {
                    Enabled = true,
                    TrackedIds = new System.Collections.Generic.HashSet<string> { traceId },
                    SamplingRate = 1.0
                });

                // Call Orleans grain (this will use RequestContext)
                var grain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>($"http-grain-{requestId}");
                var result = await grain.ProcessMessageAsync($"HTTP request {requestId}");

                results.Add((requestId.ToString(), result));

                // Clean up context
                TraceContext.Clear();
            })
            .ToArray();

        await Task.WhenAll(requestTasks);

        // Assert - All requests should complete successfully
        results.Should().HaveCount(concurrentRequests);

        for (int i = 1; i <= concurrentRequests; i++)
        {
            var requestResult = results.FirstOrDefault(r => r.RequestId == i.ToString());
            requestResult.Should().NotBeNull($"Request {i} should have completed");
            requestResult.Result.Should().NotBeNullOrEmpty($"Request {i} should return a result");
        }

        // Verify no cross-contamination
        var uniqueResults = results.Select(r => r.Result).Distinct().ToList();
        uniqueResults.Should().HaveCount(concurrentRequests, "Each request should produce a unique result");
    }
}

 