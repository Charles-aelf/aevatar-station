using Orleans;
using Orleans.Streams;
using Microsoft.Extensions.Logging;

namespace E2E.Grains;

/// <summary>
/// Single grain that can publish and subscribe to any message type
/// Uses grain ID as stream ID for simplicity
/// </summary>
public interface IStreamTestGrain : IGrainWithGuidKey
{
    Task PublishAsync<T>(T item);
    Task<List<string>> GetReceivedMessagesAsync();
    Task ClearMessagesAsync();
}

/// <summary>
/// Generic observer that can handle any event type and forward to the grain event handler
/// </summary>
public class StreamEventObserver<T> : IAsyncObserver<T>
{
    private readonly Func<T, Task> _onNextHandler;
    private readonly ILogger _logger;

    public StreamEventObserver(Func<T, Task> onNextHandler, ILogger logger)
    {
        _onNextHandler = onNextHandler;
        _logger = logger;
    }

    public async Task OnNextAsync(T item, StreamSequenceToken? token = null)
    {
        await _onNextHandler(item);
    }

    public async Task OnCompletedAsync()
    {
        _logger.LogInformation("Stream completed for {Type}", typeof(T).Name);
        await Task.CompletedTask;
    }

    public async Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Stream error for {Type}: {Message}", typeof(T).Name, ex.Message);
        await Task.CompletedTask;
    }
}

/// <summary>
/// Single grain that can publish any type and subscribe to any type
/// Uses grain ID as stream ID - one grain = one stream
/// </summary>
public class StreamTestGrain : Grain, IStreamTestGrain
{
    private readonly ILogger<StreamTestGrain> _logger;
    private readonly List<string> _receivedMessages = new();

    public StreamTestGrain(ILogger<StreamTestGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StreamTestGrain {GrainId} activated", this.GetGrainId());
        
        // Subscribe to both int and string streams during activation
        var streamProvider = this.GetStreamProvider("Aevatar");
        var streamId = StreamId.Create("TestNamespace", this.GetGrainId().ToString());
        
        // Subscribe to int stream
        var intStream = streamProvider.GetStream<int>(streamId);
        var intObserver = new StreamEventObserver<int>(
            onNextHandler: HandleIntEventAsync,
            logger: _logger);
        await intStream.SubscribeAsync(intObserver);
        
        // Subscribe to string stream
        var stringStream = streamProvider.GetStream<string>(streamId);
        var stringObserver = new StreamEventObserver<string>(
            onNextHandler: HandleStringEventAsync,
            logger: _logger);
        await stringStream.SubscribeAsync(stringObserver);
        
        _logger.LogInformation("Subscribed to both int and string streams for {GrainId}", this.GetGrainId());
        
        await base.OnActivateAsync(cancellationToken);
    }

    public async Task PublishAsync<T>(T item)
    {
        var streamProvider = this.GetStreamProvider("Aevatar");
        var streamId = StreamId.Create("TestNamespace", this.GetGrainId().ToString());
        var stream = streamProvider.GetStream<T>(streamId);
        await stream.OnNextAsync(item);
        _logger.LogInformation("Published {Type} message: {Message}", typeof(T).Name, item?.ToString());
    }


    // Event handlers for specific types
    private async Task HandleIntEventAsync(int item)
    {
        var message = $"Grain {this.GetGrainId()} received int: {item}";
        _receivedMessages.Add(message);
        _logger.LogInformation(message);
        await Task.CompletedTask;
    }

    private async Task HandleStringEventAsync(string item)
    {
        var message = $"Grain {this.GetGrainId()} received string: {item}";
        _receivedMessages.Add(message);
        _logger.LogInformation(message);
        await Task.CompletedTask;
    }


    public Task<List<string>> GetReceivedMessagesAsync()
    {
        return Task.FromResult(new List<string>(_receivedMessages));
    }

    public Task ClearMessagesAsync()
    {
        _receivedMessages.Clear();
        return Task.CompletedTask;
    }
}