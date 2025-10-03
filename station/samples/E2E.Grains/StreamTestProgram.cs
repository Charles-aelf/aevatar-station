using Microsoft.Extensions.Logging;
using Orleans;

namespace E2E.Grains;

/// <summary>
/// Program class for testing the StreamTestGrain
/// </summary>
public static class StreamTestProgram
{
    public static async Task RunAsync(IGrainFactory grainFactory, ILogger logger)
    {
        logger.LogInformation("Starting StreamTestGrain demo");

        // Create a test grain - it will automatically subscribe to both int and string streams during activation
        var testGrain = grainFactory.GetGrain<IStreamTestGrain>(Guid.NewGuid());
        
        // Clear any previous messages
        await testGrain.ClearMessagesAsync();
        
        // Wait a moment for subscriptions to be established
        await Task.Delay(1000);
        
        // Publish some int messages
        logger.LogInformation("Publishing int messages...");
        await testGrain.PublishAsync(42);
        await testGrain.PublishAsync(100);
        await testGrain.PublishAsync(-5);
        
        // Publish some string messages
        logger.LogInformation("Publishing string messages...");
        await testGrain.PublishAsync("Hello World!");
        await testGrain.PublishAsync("Orleans Streams");
        await testGrain.PublishAsync("Type Mismatch Test");
        
        // Wait for messages to be processed
        await Task.Delay(2000);
        
        // Get and display received messages
        var receivedMessages = await testGrain.GetReceivedMessagesAsync();
        
        logger.LogInformation("Received {Count} messages:", receivedMessages.Count);
        foreach (var message in receivedMessages)
        {
            logger.LogInformation("  - {Message}", message);
        }
        
        logger.LogInformation("StreamTestGrain demo completed");
    }
}
