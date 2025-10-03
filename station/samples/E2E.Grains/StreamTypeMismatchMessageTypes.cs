using Orleans.Streams;

namespace E2E.Grains;

/// <summary>
/// First message type for testing stream type mismatch scenarios
/// </summary>
[GenerateSerializer]
public record Class1
{
    [Id(0)]
    public string Name { get; init; } = string.Empty;
    
    [Id(1)]
    public int Value { get; init; }
    
    [Id(2)]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Second message type for testing stream type mismatch scenarios
/// </summary>
[GenerateSerializer]
public record Class2
{
    [Id(0)]
    public string Description { get; init; } = string.Empty;
    
    [Id(1)]
    public double Amount { get; init; }
    
    [Id(2)]
    public bool IsActive { get; init; }
}

/// <summary>
/// Third message type for testing stream type mismatch scenarios
/// </summary>
[GenerateSerializer]
public record Class3
{
    [Id(0)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    [Id(1)]
    public List<string> Tags { get; init; } = new();
    
    [Id(2)]
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Base interface for all message types - just to document common properties
/// </summary>
public interface IStreamMessage
{
    string MessageType { get; }
    DateTime CreatedAt { get; }
}

/// <summary>
/// Wrapper message that can contain any of the specific message types
/// </summary>
[GenerateSerializer]
public record StreamMessageWrapper : IStreamMessage
{
    [Id(0)]
    public string MessageType { get; init; } = string.Empty;
    
    [Id(1)]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    [Id(2)]
    public object Payload { get; init; } = new();
    
    public static StreamMessageWrapper Create<T>(T payload) where T : class
    {
        return new StreamMessageWrapper
        {
            MessageType = typeof(T).Name,
            Payload = payload
        };
    }
}
