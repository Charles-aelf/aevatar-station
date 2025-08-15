using Xunit;
using FluentAssertions;
using System.Collections.Generic;
using Aevatar.Core.Tracing;
using Aevatar.Core.Abstractions.Tracing;

namespace Aevatar.Tracing.E2E.Tests;

/// <summary>
/// Tests to validate the updated tracing behavior with secure defaults and runtime management.
/// </summary>
public class TracingBehaviorTests
{
    [Fact]
    public void TraceConfig_Should_Not_Trace_By_Default()
    {
        // Arrange
        var config = new TraceConfig { Enabled = true };

        // Act & Assert - Should not trace when no TrackedIds are configured (secure by default)
        config.ShouldTrace("any-id").Should().BeFalse("no TrackedIds configured means no tracing");
        config.ShouldTrace("test-123").Should().BeFalse("no TrackedIds configured means no tracing");
        config.ShouldTrace(null).Should().BeFalse("null ID should never be traced");
        config.ShouldTrace("").Should().BeFalse("empty ID should never be traced");
    }

    [Fact]
    public void TraceConfig_Should_Trace_Only_When_ID_In_TrackedIds()
    {
        // Arrange
        var config = new TraceConfig 
        { 
            Enabled = true,
            TrackedIds = new HashSet<string> { "trace-1", "trace-2" }
        };

        // Act & Assert
        config.ShouldTrace("trace-1").Should().BeTrue("ID is in TrackedIds");
        config.ShouldTrace("trace-2").Should().BeTrue("ID is in TrackedIds");
        config.ShouldTrace("trace-3").Should().BeFalse("ID is not in TrackedIds");
        config.ShouldTrace("unknown").Should().BeFalse("ID is not in TrackedIds");
    }

    [Fact]
    public void TraceConfig_AddTrackedId_Should_Enable_Tracing_For_New_ID()
    {
        // Arrange
        var config = new TraceConfig { Enabled = true };

        // Initially should not trace anything
        config.ShouldTrace("test-id").Should().BeFalse();

        // Act
        var added = config.AddTrackedId("test-id");

        // Assert
        added.Should().BeTrue("new ID should be added");
        config.ShouldTrace("test-id").Should().BeTrue("ID should now be traced");
        config.ShouldTrace("other-id").Should().BeFalse("other IDs should not be traced");
    }

    [Fact]
    public void TraceConfig_AddTrackedId_Should_Return_False_For_Duplicate()
    {
        // Arrange
        var config = new TraceConfig { Enabled = true };
        config.AddTrackedId("test-id");

        // Act
        var addedAgain = config.AddTrackedId("test-id");

        // Assert
        addedAgain.Should().BeFalse("duplicate ID should not be added");
        config.ShouldTrace("test-id").Should().BeTrue("ID should still be traced");
    }

    [Fact]
    public void TraceConfig_RemoveTrackedId_Should_Disable_Tracing()
    {
        // Arrange
        var config = new TraceConfig 
        { 
            Enabled = true,
            TrackedIds = new HashSet<string> { "trace-1", "trace-2" }
        };

        // Act
        var removed = config.RemoveTrackedId("trace-1");

        // Assert
        removed.Should().BeTrue("ID should be removed");
        config.ShouldTrace("trace-1").Should().BeFalse("removed ID should not be traced");
        config.ShouldTrace("trace-2").Should().BeTrue("other IDs should still be traced");
    }

    [Fact]
    public void TraceConfig_ClearTrackedIds_Should_Disable_All_Tracing()
    {
        // Arrange
        var config = new TraceConfig 
        { 
            Enabled = true,
            TrackedIds = new HashSet<string> { "trace-1", "trace-2", "trace-3" }
        };

        // Act
        config.ClearTrackedIds();

        // Assert
        config.ShouldTrace("trace-1").Should().BeFalse("all IDs should be cleared");
        config.ShouldTrace("trace-2").Should().BeFalse("all IDs should be cleared");
        config.ShouldTrace("trace-3").Should().BeFalse("all IDs should be cleared");
    }

    [Fact]
    public void TraceContext_EnableTracing_Should_Set_Active_ID_And_Add_To_TrackedIds()
    {
        // Arrange
        TraceContext.Clear();

        // Act
        var enabled = TraceContext.EnableTracing("dynamic-trace-123");

        // Assert
        enabled.Should().BeTrue("tracing should be enabled");
        TraceContext.ActiveTraceId.Should().Be("dynamic-trace-123");
        TraceContext.IsTracingEnabled.Should().BeTrue("tracing should be active");
        TraceContext.GetTrackedIds().Should().Contain("dynamic-trace-123");

        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public void TraceContext_DisableTracing_Should_Remove_From_TrackedIds()
    {
        // Arrange
        TraceContext.Clear();
        TraceContext.EnableTracing("test-trace");

        // Act
        var disabled = TraceContext.DisableTracing("test-trace");

        // Assert
        disabled.Should().BeTrue("tracing should be disabled");
        TraceContext.ActiveTraceId.Should().BeNull("active trace ID should be cleared");
        TraceContext.IsTracingEnabled.Should().BeFalse("tracing should be inactive");
        TraceContext.GetTrackedIds().Should().NotContain("test-trace");

        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public void TraceContext_AddTrackedId_Should_Enable_Tracing_Without_Setting_Active()
    {
        // Arrange
        TraceContext.Clear();

        // Act
        var added = TraceContext.AddTrackedId("background-trace");

        // Assert
        added.Should().BeTrue("ID should be added");
        TraceContext.ActiveTraceId.Should().BeNull("active trace ID should not be set");
        TraceContext.GetTrackedIds().Should().Contain("background-trace");

        // Now set it as active
        TraceContext.ActiveTraceId = "background-trace";
        TraceContext.IsTracingEnabled.Should().BeTrue("tracing should work when ID becomes active");

        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public void TraceContext_Multiple_TrackedIds_Should_Work_Independently()
    {
        // Arrange
        TraceContext.Clear();

        // Act
        TraceContext.AddTrackedId("trace-1");
        TraceContext.AddTrackedId("trace-2");
        TraceContext.AddTrackedId("trace-3");

        // Assert
        var trackedIds = TraceContext.GetTrackedIds();
        trackedIds.Should().HaveCount(3);
        trackedIds.Should().Contain("trace-1");
        trackedIds.Should().Contain("trace-2");
        trackedIds.Should().Contain("trace-3");

        // Test each ID works when active
        TraceContext.ActiveTraceId = "trace-1";
        TraceContext.IsTracingEnabled.Should().BeTrue();

        TraceContext.ActiveTraceId = "trace-2";
        TraceContext.IsTracingEnabled.Should().BeTrue();

        TraceContext.ActiveTraceId = "trace-3";
        TraceContext.IsTracingEnabled.Should().BeTrue();

        TraceContext.ActiveTraceId = "unknown-trace";
        TraceContext.IsTracingEnabled.Should().BeFalse();

        // Cleanup
        TraceContext.Clear();
    }

    [Fact]
    public void TraceContext_Should_Handle_Invalid_Inputs_Gracefully()
    {
        // Arrange
        TraceContext.Clear();

        // Act & Assert
        TraceContext.EnableTracing(null).Should().BeFalse();
        TraceContext.EnableTracing("").Should().BeFalse();
        TraceContext.EnableTracing("   ").Should().BeFalse();

        TraceContext.AddTrackedId(null).Should().BeFalse();
        TraceContext.AddTrackedId("").Should().BeFalse();

        TraceContext.RemoveTrackedId(null).Should().BeFalse();
        TraceContext.RemoveTrackedId("").Should().BeFalse();

        TraceContext.DisableTracing(null).Should().BeFalse();
        TraceContext.DisableTracing("").Should().BeFalse();

        // Cleanup
        TraceContext.Clear();
    }
} 