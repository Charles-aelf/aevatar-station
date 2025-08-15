using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.TestingHost;
using Orleans.Hosting;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Aevatar.Core.Tracing;
using Aevatar.Core.Abstractions.Tracing;
using Aevatar.Tracing.E2E.Tests.TestGrains;
using Aevatar.Silo.Extensions;

namespace Aevatar.Tracing.E2E.Tests;

/// <summary>
/// End-to-end tests for method-level comprehensive tracing.
/// Tests the complete flow: HTTP → Grain → Grain with trace context propagation.
/// </summary>
public class MethodLevelTracingE2ETests : IClassFixture<TracingTestCluster>
{
    private readonly TracingTestCluster _cluster;

    public MethodLevelTracingE2ETests(TracingTestCluster cluster)
    {
        _cluster = cluster;
    }

    [Fact]
    public async Task Should_Propagate_TraceContext_Across_Grain_Calls()
    {
        // Arrange
        var testTraceId = "test-trace-123";
        var config = new TraceConfig 
        { 
            Enabled = true,
            TrackedIds = new HashSet<string> { testTraceId }
        };

        // Set trace context manually (simulating HTTP middleware)
        TraceContext.ActiveTraceId = testTraceId;
        TraceContext.SetTraceConfig(config);

        var grain1 = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("grain1");
        var grain2 = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("grain2");

        // Act
        var result = await grain1.CallDownstreamGrainAsync("grain2", "Hello from grain1");

        // Assert
        result.Should().Contain(testTraceId);
        result.Should().Contain("Upstream");
        result.Should().Contain("Processed: Forwarded: Hello from grain1");
        
        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public async Task Should_Trace_Method_With_Parameters_And_ReturnValue()
    {
        // Arrange
        var testTraceId = "param-test-456";
        var config = new TraceConfig 
        { 
            Enabled = true,
            TrackedIds = new HashSet<string> { testTraceId }
        };

        TraceContext.ActiveTraceId = testTraceId;
        TraceContext.SetTraceConfig(config);

        var grain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("test-grain");
        var testData = new TestData("data-123", "Test Data", 42, DateTime.UtcNow);

        // Act
        var result = await grain.ProcessComplexDataAsync(testData);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ProcessedId.Should().StartWith("data-123-processed-");
        result.Message.Should().Contain(testTraceId);
        result.ProcessingTime.Should().BeGreaterThan(TimeSpan.Zero);

        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public async Task Should_Trace_Exception_With_Context()
    {
        // Arrange
        var testTraceId = "error-test-789";
        var config = new TraceConfig 
        { 
            Enabled = true,
            TrackedIds = new HashSet<string> { testTraceId },
            CaptureExceptionStackTrace = true
        };

        TraceContext.ActiveTraceId = testTraceId;
        TraceContext.SetTraceConfig(config);

        var grain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("error-grain");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.ThrowExceptionAsync("Test error message"));

        exception.Message.Should().Contain("Test error message");
        exception.Message.Should().Contain(testTraceId);

        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public async Task Should_Not_Trace_When_TraceId_Not_In_TrackedIds()
    {
        // Arrange
        var testTraceId = "untracked-trace-999";
        var config = new TraceConfig 
        { 
            Enabled = true,
            TrackedIds = new HashSet<string> { "different-trace-id" } // Different ID
        };

        TraceContext.ActiveTraceId = testTraceId;
        TraceContext.SetTraceConfig(config);

        var grain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("untracked-grain");

        // Act
        var result = await grain.ProcessMessageAsync("Should not be traced");

        // Assert
        result.Should().Contain(testTraceId); // Grain should still see the trace ID
        // But tracing should not be enabled
        TraceContext.IsTracingEnabled.Should().BeFalse();

        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public async Task Should_Not_Trace_When_Globally_Disabled()
    {
        // Arrange
        var testTraceId = "disabled-trace-111";
        var config = new TraceConfig 
        { 
            Enabled = false, // Globally disabled
            TrackedIds = new HashSet<string> { testTraceId }
        };

        TraceContext.ActiveTraceId = testTraceId;
        TraceContext.SetTraceConfig(config);

        var grain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("disabled-grain");

        // Act
        var result = await grain.ProcessMessageAsync("Should not be traced");

        // Assert
        result.Should().Contain(testTraceId); // Grain should still see the trace ID
        TraceContext.IsTracingEnabled.Should().BeFalse(); // But tracing should be disabled

        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public async Task Should_Handle_Multiple_Concurrent_Traces()
    {
        // Arrange
        var tasks = new List<Task<string>>();
        var config = new TraceConfig 
        { 
            Enabled = true
        };

        // Act - Start multiple concurrent grain calls with different trace IDs
        for (int i = 0; i < 10; i++)
        {
            var traceId = $"concurrent-trace-{i}";
            tasks.Add(ProcessWithTraceId(traceId, config, i));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        for (int i = 0; i < 10; i++)
        {
            results[i].Should().Contain($"concurrent-trace-{i}");
            results[i].Should().Contain($"Message {i}");
        }
    }

    private async Task<string> ProcessWithTraceId(string traceId, TraceConfig config, int messageIndex)
    {
        // Each task gets its own trace context
        TraceContext.ActiveTraceId = traceId;
        TraceContext.SetTraceConfig(config);

        try
        {
            var grain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>($"concurrent-grain-{messageIndex}");
            return await grain.ProcessMessageAsync($"Message {messageIndex}");
        }
        finally
        {
            TraceContext.Clear();
        }
    }
}

/// <summary>
/// Test cluster configuration for E2E tracing tests.
/// </summary>
public class TracingTestCluster : IDisposable
{
    public TestCluster Cluster { get; }
    public IGrainFactory GrainFactory => Cluster.GrainFactory;

    public TracingTestCluster()
    {
        var builder = new TestClusterBuilder();
        
        builder.AddSiloBuilderConfigurator<SiloConfigurator>();
        
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster?.StopAllSilos();
        Cluster?.Dispose();
    }

    private class SiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder
                .AddMethodTracing() // Add our tracing extensions
                .ConfigureServices(services =>
                {
                    // Register test grains
                    services.AddTransient<TestTraceGrain>();
                    
                    // Configure OpenTelemetry for testing
                    services.AddOpenTelemetry()
                        .WithTracing(builder =>
                        {
                            builder
                                .AddSource("Aevatar.MethodTracing")
                                .SetSampler(new AlwaysOnSampler());
                        });
                });
        }
    }
} 