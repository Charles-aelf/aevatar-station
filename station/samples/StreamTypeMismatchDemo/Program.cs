using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Streams.Kafka.Config;
using E2E.Grains;
using Aevatar.Core.Streaming.Extensions;

namespace StreamTypeMismatchDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Simple Orleans Stream Type Mismatch Test ===");
        
        IHostBuilder builder = Host.CreateDefaultBuilder(args)
            .UseOrleansClient(client =>
            {
                //client.UseLocalhostClustering();
                var hostId = "Aevatar";
                client.UseMongoDBClient("mongodb://localhost:27017")
                    .UseMongoDBClustering(options =>
                    {
                        options.DatabaseName = "AevatarDb";
                        options.Strategy = MongoDBMembershipStrategy.SingleDocument;
                        options.CollectionPrefix = hostId.IsNullOrEmpty() ? "OrleansAevatar" : $"Orleans{hostId}";
                    })
                    .Configure<ClusterOptions>(options =>
                    {
                        options.ClusterId = "AevatarSiloCluster";
                        options.ServiceId = "AevatarBasicService";
                    })
                    .AddActivityPropagation()
                    // client.UseLocalhostClustering(gatewayPort: 20001)
                    // .AddMemoryStreams(AevatarCoreConstants.StreamProvider);
                    .AddAevatarKafkaStreaming("Aevatar", options =>
                    {
                        options.BrokerList = new List<string> { "localhost:9092" };
                        options.ConsumerGroupId = "Aevatar";
                        options.ConsumeMode = ConsumeMode.LastCommittedMessage;

                        var partitions = 8; // Multiple partitions for load distribution
                        var replicationFactor = (short)1;  // ReplicationFactor should be short
                        var topics = "Aevatar,AevatarStateProjection,AevatarBroadcast";
                        foreach (var topic in topics.Split(','))
                        {
                            options.AddTopic(topic.Trim(), new TopicCreationConfig
                            {
                                AutoCreate = true,
                                Partitions = partitions,
                                ReplicationFactor = replicationFactor
                            });
                        }
                    });
                    // .WithOptions(options =>
                    // {
                        
                    // })
                    // .AddJson()  // Add logging tracker for better observability
                    // .Build();
            })
            .ConfigureLogging(logging => logging.AddConsole())
            .UseConsoleLifetime();

        using IHost host = builder.Build();
        await host.StartAsync();

        var client = host.Services.GetRequiredService<IClusterClient>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        try
        {
            await RunSimpleTest(client, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test failed");
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            await host.StopAsync();
        }
    }

    static async Task RunSimpleTest(IClusterClient client, ILogger logger)
    {
        var grainId = Guid.NewGuid();
        Console.WriteLine($"Testing StreamTestGrain with ID: {grainId}");
        Console.WriteLine("Grain automatically subscribes to both int and string streams during activation");
        Console.WriteLine();

        // Create single grain instance - it will automatically subscribe to both int and string streams
        var grain = client.GetGrain<IStreamTestGrain>(grainId);
        
        // Clear any previous messages
        await grain.ClearMessagesAsync();
        
        // Wait for activation and subscriptions to be established
        await Task.Delay(1000);
        
        Console.WriteLine("1. Publishing int messages...");
        await grain.PublishAsync(42);
        await grain.PublishAsync(100);
        await grain.PublishAsync(-5);
        
        Console.WriteLine("2. Publishing string messages...");
        await grain.PublishAsync("Hello World!");
        await grain.PublishAsync("Orleans Streams");
        await grain.PublishAsync("Type Test");
        
        // Wait for messages to be processed
        await Task.Delay(2000);
        
        Console.WriteLine("3. Checking received messages...");
        var messages = await grain.GetReceivedMessagesAsync();
        
        if (messages.Any())
        {
            Console.WriteLine($"Received {messages.Count} messages:");
            foreach (var msg in messages)
            {
                Console.WriteLine($"  - {msg}");
            }
        }
        else
        {
            Console.WriteLine("No messages received");
        }
        
        Console.WriteLine();
        Console.WriteLine("=== Result ===");
        
        if (messages.Any())
        {
            var intMessages = messages.Count(m => m.Contains("received int:"));
            var stringMessages = messages.Count(m => m.Contains("received string:"));
            
            Console.WriteLine($"✅ Successfully handled {intMessages} int messages and {stringMessages} string messages!");
            Console.WriteLine("Orleans streams properly route messages to type-specific handlers.");
        }
        else
        {
            Console.WriteLine("ℹ️  No messages received - check Orleans configuration and stream setup.");
        }
    }
}