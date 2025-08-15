# Method-Level Tracing - Quick Reference

## 🚀 Quick Start

### 1. Add to Silo
```csharp
builder.AddMethodTracing();
```

### 2. Add to Web API
```csharp
app.UseTraceContext();
```

### 3. Mark Methods for Tracing
```csharp
[Trace("OperationName", CaptureParameters = true, CaptureReturnValue = true)]
public async Task<Result> TracedMethodAsync(Request request)
{
    // Your code here
}
```

### 4. Send Traced Request
```
GET /api/endpoint?traceId=user123
# or
GET /api/endpoint
X-Trace-Id: user123
```

## 📋 TraceAttribute Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `OperationName` | string | method name | Name for the trace span |
| `CaptureParameters` | bool | false | Include method parameters in trace |
| `CaptureReturnValue` | bool | false | Include return value in trace |
| `CaptureLocalVariables` | bool | false | Include local variables (expensive) |
| `CaptureStackTrace` | bool | true | Include stack trace on exceptions |

## ⚙️ Configuration

### TraceConfig Settings
```csharp
var config = new TraceConfig
{
    Enabled = true,                                    // Enable/disable tracing
    TrackedIds = new HashSet<string> { "user123" },   // Specific IDs to trace
    SamplingRate = 1.0,                               // 0.0 to 1.0 (100%)
    MaxSpansPerTrace = 1000,                          // Limit spans per trace
    CaptureParameters = false,                        // Global parameter capture
    CaptureReturnValue = false,                       // Global return value capture
    CaptureLocalVariables = false                     // Global local variable capture
};
```

### Service Registration
```csharp
// Register services for tracing (required for interception)
services.AddProxiedScoped<IOrderService, OrderService>();
services.AddProxiedTransient<IPaymentService, PaymentService>();
services.AddProxiedSingleton<ICacheService, CacheService>();
```

## 🔄 Context Propagation

### Automatic Flow
```
HTTP Request → Middleware → AsyncLocal → Service → OutgoingFilter → 
RequestContext → TargetGrain → IncomingFilter → AsyncLocal → GrainMethod
```

### Manual Context Management
```csharp
// Check current trace
var traceId = TraceContext.ActiveTraceId;
var isEnabled = TraceContext.IsTracingEnabled;

// Set trace context manually
TraceContext.SetTraceId("custom-trace-123");
TraceContext.SetTraceConfig(config);

// Clear context
TraceContext.Clear();
```

## 🌐 HTTP Integration

### Extract Trace ID From:

1. **Query Parameter**: `?traceId=user123`
2. **Header**: `X-Trace-Id: user123`
3. **Route**: `/api/orders/{orderId}/trace/{traceId}`

### Custom Middleware Configuration
```csharp
app.UseTraceContext(options =>
{
    options.HeaderName = "X-Custom-Trace-Id";
    options.QueryParameterName = "customTraceId";
    options.RouteParameterName = "customTraceId";
    options.DefaultConfig = new TraceConfig { Enabled = true };
});
```

## 🏗️ Orleans Integration

### Grain Example
```csharp
public interface IOrderGrain : IGrainWithStringKey
{
    Task<Order> CreateOrderAsync(OrderRequest request);
}

public class OrderGrain : Grain, IOrderGrain
{
    [Trace("CreateOrder", CaptureParameters = true)]
    public async Task<Order> CreateOrderAsync(OrderRequest request)
    {
        // Trace context automatically available
        var traceId = TraceContext.ActiveTraceId;
        
        // Call other grains - context propagates automatically
        var paymentGrain = GrainFactory.GetGrain<IPaymentGrain>(request.PaymentId);
        await paymentGrain.ProcessAsync(request.Payment);
        
        return new Order { /* ... */ };
    }
}
```

### Filters Registration
```csharp
// Automatic with AddMethodTracing()
builder.AddMethodTracing();

// Manual registration (if needed)
builder
    .AddIncomingGrainCallFilter<TraceIncomingGrainCallFilter>()
    .AddOutgoingGrainCallFilter<TraceOutgoingGrainCallFilter>();
```

## 📊 OpenTelemetry Integration

### Basic Setup
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Aevatar.MethodTracing");
        tracing.AddOtlpExporter();
    });
```

### Custom Processing
```csharp
public class CustomTraceProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
        if (activity.Source.Name == "Aevatar.MethodTracing")
        {
            activity.SetTag("environment", "production");
        }
    }
}

tracing.AddProcessor<CustomTraceProcessor>();
```

## 🧪 Testing

### Unit Test Example
```csharp
[Test]
public async Task Should_Trace_Method_Execution()
{
    // Arrange
    TraceContext.SetTraceId("test-trace");
    TraceContext.SetTraceConfig(new TraceConfig { Enabled = true });
    
    // Act
    var result = await _service.ProcessAsync(request);
    
    // Assert
    Assert.NotNull(result);
    // Verify trace data in your test exporter
}
```

### E2E Test Structure
```csharp
[Fact]
public async Task Should_Propagate_Context_Through_Grain_Calls()
{
    // Set trace context
    var testGrain = _cluster.GrainFactory.GetGrain<ITestTraceGrain>("test");
    
    // Execute with trace ID
    var result = await testGrain.CallChainAsync("trace-123", "test message");
    
    // Verify trace correlation
    result.TraceId.Should().Be("trace-123");
}
```

## 🐛 Common Issues

### No Traces Generated
```csharp
// ✅ Check: Service registered with proxy
services.AddProxiedScoped<IService, Service>();

// ✅ Check: Filters registered
builder.AddMethodTracing();

// ✅ Check: Trace context set
TraceContext.SetTraceId("test-123");

// ✅ Check: Method has attribute
[Trace("MethodName")]
```

### Context Not Propagating
```csharp
// ✅ Ensure filters are registered in silo
builder.AddMethodTracing();

// ✅ Check Orleans context availability
var isOrleansContext = TraceContext.IsOrleansContextAvailable();

// ✅ Verify RequestContext serialization
[GenerateSerializer]
public class TraceConfig { /* with [Id(n)] attributes */ }
```

### Performance Issues
```csharp
// ✅ Reduce sampling rate
config.SamplingRate = 0.1; // 10% of requests

// ✅ Disable expensive captures
config.CaptureParameters = false;
config.CaptureLocalVariables = false;

// ✅ Limit spans per trace
config.MaxSpansPerTrace = 100;
```

## 💡 Best Practices

### Security
```csharp
// ❌ Don't trace sensitive methods
[Trace("ProcessPayment", CaptureParameters = false, CaptureReturnValue = false)]

// ✅ Use generic trace IDs
// Use: customer-session-123
// Not: customer-ssn-123456789
```

### Performance
```csharp
// ✅ Use sampling for high-volume methods
[Trace("HighVolumeMethod", CaptureParameters = false)]

// ✅ Selective parameter capture
[Trace("Method", CaptureParameters = true)]
public async Task ProcessAsync(
    string publicId,           // ✅ Safe to capture
    [DoNotCapture] string secretKey  // Custom attribute to exclude
) { }
```

### Debugging
```csharp
// ✅ Enable debug logging
builder.Logging.AddFilter("Aevatar.Tracing", LogLevel.Debug);

// ✅ Use descriptive operation names
[Trace("ProcessOrderPayment")] // ✅ Clear
[Trace("Process")] // ❌ Too generic

// ✅ Include business context in trace IDs
"order-process-{orderId}"
"user-session-{sessionId}"
"batch-job-{batchId}"
```

## 📚 Links

- [Detailed Documentation](./method-level-tracing-readme.md)
- [Architecture Design](./method-level-comprehensive-tracing-design.md)
- [E2E Tests](../station/test/Aevatar.Tracing.E2E.Tests/)
- [OpenTelemetry Docs](https://opentelemetry.io/docs/) 