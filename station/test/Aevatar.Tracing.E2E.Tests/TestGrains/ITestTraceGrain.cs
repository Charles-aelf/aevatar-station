using Orleans;

namespace Aevatar.Tracing.E2E.Tests.TestGrains;

/// <summary>
/// Test data structure for grain interface testing.
/// </summary>
[GenerateSerializer]
public class TestData
{
    [Id(0)]
    public string Id { get; set; } = string.Empty;
    
    [Id(1)]
    public string Name { get; set; } = string.Empty;
    
    [Id(2)]
    public int Value { get; set; }
    
    [Id(3)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Parameterless constructor for Orleans serialization
    public TestData() { }
    
    // Constructor to match the test usage
    public TestData(string id, string name, int value, DateTime timestamp)
    {
        Id = id;
        Name = name;
        Value = value;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Test result structure for grain interface testing.
/// </summary>
[GenerateSerializer]
public class TestResult
{
    [Id(0)]
    public string ProcessedId { get; set; } = string.Empty;
    
    [Id(1)]
    public string Message { get; set; } = string.Empty;
    
    [Id(2)]
    public bool Success { get; set; }
    
    [Id(3)]
    public TimeSpan ProcessingTime { get; set; }
    
    // Parameterless constructor for Orleans serialization
    public TestResult() { }
    
    // Constructor to match the grain implementation
    public TestResult(string processedId, string message, bool success, TimeSpan processingTime)
    {
        ProcessedId = processedId;
        Message = message;
        Success = success;
        ProcessingTime = processingTime;
    }
}

/// <summary>
/// Test grain interface for verifying tracing across grain calls.
/// </summary>
public interface ITestTraceGrain : IGrainWithStringKey
{
    /// <summary>
    /// Simple method with trace attribute for testing basic tracing.
    /// </summary>
    /// <param name="message">Test message to process.</param>
    /// <returns>Processed message with trace information.</returns>
    Task<string> ProcessMessageAsync(string message);

    /// <summary>
    /// Method that calls another grain to test trace context propagation.
    /// </summary>
    /// <param name="targetGrainId">ID of target grain to call.</param>
    /// <param name="message">Message to forward.</param>
    /// <returns>Response from downstream grain.</returns>
    Task<string> CallDownstreamGrainAsync(string targetGrainId, string message);

    /// <summary>
    /// Method that throws an exception to test exception tracing.
    /// </summary>
    /// <param name="errorMessage">Error message to throw.</param>
    /// <returns>Should throw an exception.</returns>
    Task<string> ThrowExceptionAsync(string errorMessage);

    /// <summary>
    /// Method with complex parameters to test parameter capture.
    /// </summary>
    /// <param name="complexData">Complex test data.</param>
    /// <returns>Processed result.</returns>
    Task<TestResult> ProcessComplexDataAsync(TestData complexData);
} 