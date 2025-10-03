# Orleans Stream Type Mismatch Demo

This demo tests what happens when different message types are published to the same stream ID and consumed by grains expecting specific types. It validates whether Orleans throws runtime exceptions during deserialization when there's a type mismatch.

## Overview

Orleans streams are strongly typed, and when you subscribe to a stream with a specific generic type `T`, you expect to receive messages of that type. However, if different message types are published to the same stream ID, this can lead to runtime exceptions during deserialization.

## What This Demo Tests

1. **Multiple Message Types**: Publishes `Class1`, `Class2`, and `Class3` messages to the same stream ID
2. **Type-Specific Consumers**: Creates separate consumer grains that expect specific message types:
   - `Class1ConsumerGrain` expects `Class1` messages
   - `Class2ConsumerGrain` expects `Class2` messages  
   - `Class3ConsumerGrain` expects `Class3` messages
3. **Runtime Exception Detection**: Monitors for deserialization errors when type mismatches occur

## Message Types

### Class1
```csharp
public record Class1
{
    public string Name { get; init; }
    public int Value { get; init; }
    public DateTime Timestamp { get; init; }
}
```

### Class2
```csharp
public record Class2
{
    public string Description { get; init; }
    public double Amount { get; init; }
    public bool IsActive { get; init; }
}
```

### Class3
```csharp
public record Class3
{
    public Guid Id { get; init; }
    public List<string> Tags { get; init; }
    public Dictionary<string, object> Metadata { get; init; }
}
```

## Architecture

```
┌─────────────────┐    ┌─────────────────┐
│  Publisher      │    │  Stream         │
│  Grain          │───▶│  (Same ID)      │
│                 │    │                 │
└─────────────────┘    └─────────────────┘
                                │
                                ▼
                    ┌─────────────────────┐
                    │                     │
            ┌───────┴────────┐    ┌──────┴────────┐    ┌──────┴────────┐
            │ Class1Consumer │    │ Class2Consumer │    │ Class3Consumer │
            │ (expects T1)   │    │ (expects T2)   │    │ (expects T3)   │
            └────────────────┘    └────────────────┘    └────────────────┘
```

## Expected Behavior

### Scenario 1: Type Mismatch Runtime Exceptions
If Orleans throws runtime exceptions during deserialization:
- Each consumer will only receive messages of their expected type
- Consumers will encounter deserialization errors for incompatible types
- The demo will report the number of errors encountered

### Scenario 2: Graceful Type Handling
If Orleans handles type mismatches gracefully:
- Consumers may receive all messages but only process compatible ones
- No runtime exceptions will be thrown
- The demo will report no errors

## Running the Demo

### Prerequisites
- MongoDB running on `localhost:27017`
- Kafka running on `localhost:9092`
- Orleans Silo running (gateway port 20001)

### Execution
```bash
cd station/samples/StreamTypeMismatchDemo
dotnet run
```

### Sample Output
```
=== Orleans Stream Type Mismatch Demo ===
Testing stream type mismatch with Stream ID: 12345678-1234-1234-1234-123456789abc
Publishing 15 messages (5 of each type: Class1, Class2, Class3)

Starting consumers...
Starting publisher...
Waiting for messages to be published and consumed...

=== Test Results ===
Published messages: 15

Class1 Consumer Results:
  Received messages: 5
  Errors: 10
  Messages received:
    - Received Class1: Message_0, Value: 0, Timestamp: 14:30:15.123
    - Received Class1: Message_3, Value: 3, Timestamp: 14:30:15.223
    ...
  Errors encountered:
    - Deserialization error: Type mismatch for Class2
    - Deserialization error: Type mismatch for Class3
    ...

⚠️  RUNTIME EXCEPTIONS DETECTED!
This confirms that Orleans throws runtime exceptions when there's a type mismatch
during deserialization in stream pub/sub scenarios.
```

## Key Findings

This demo helps validate:

1. **Orleans Stream Type Safety**: Whether Orleans enforces type safety at the stream level
2. **Deserialization Behavior**: How Orleans handles type mismatches during message deserialization
3. **Error Handling**: Whether runtime exceptions are thrown or handled gracefully
4. **Consumer Isolation**: Whether consumers are isolated by message type

## Implications

The results of this demo have important implications for:

- **Stream Design**: Whether to use separate streams for different message types
- **Error Handling**: How to handle type mismatches in production systems
- **Serialization Strategy**: Whether to use polymorphic serialization or type-safe streams
- **Testing Strategy**: How to test stream type compatibility

## Related Concepts

- **Orleans Streams**: Strongly typed message streaming in Orleans
- **Serialization**: How Orleans serializes/deserializes stream messages
- **Type Safety**: Compile-time vs runtime type checking in distributed systems
- **Error Handling**: Graceful degradation vs fail-fast approaches

## Troubleshooting

### Common Issues

1. **MongoDB Connection**: Ensure MongoDB is running on localhost:27017
2. **Kafka Connection**: Ensure Kafka is running on localhost:9092
3. **Orleans Silo**: Ensure Orleans silo is running and accessible
4. **Port Conflicts**: Check if port 20001 is available for the Orleans gateway

### Debug Tips

- Check the console output for detailed error messages
- Monitor Orleans logs for stream subscription and deserialization issues
- Verify that all required services (MongoDB, Kafka, Orleans) are running
- Check network connectivity between components

## Contributing

When modifying this demo:

1. Maintain the core test scenario (different types on same stream)
2. Add new message types if needed for additional testing
3. Update the documentation to reflect any changes
4. Ensure the demo remains self-contained and runnable

## References

- [Orleans Streams Documentation](https://dotnet.github.io/orleans/docs/streaming/index.html)
- [Orleans Serialization](https://dotnet.github.io/orleans/docs/grains/serialization.html)
- [Orleans Error Handling](https://dotnet.github.io/orleans/docs/grains/error-handling.html)
