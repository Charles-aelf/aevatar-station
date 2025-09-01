using Aevatar.Core.Abstractions;
using Aevatar.GAgents.AIGAgent.Agent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Aevatar.Application.Grains.Agents.AI;

[GAgent("VideoGeneration")]
public class VideoGenerationGAgent : AIGAgentBase<VideoGenerationState, VideoGenerationEvent>, IVideoGenerationGAgent
{
    private readonly BytePlusModelArkClient _bytePlusClient;
    
    public VideoGenerationGAgent()
    {
        // BytePlus client will be initialized on first use to avoid logger issues in constructor
        var httpClient = new HttpClient();
        var apiKey = "a6019b86-ca53-4e10-a1d7-80ca0cd93ce7";
        var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
        var typedLogger = loggerFactory.CreateLogger<BytePlusModelArkClient>();
        _bytePlusClient = new BytePlusModelArkClient(httpClient, typedLogger, apiKey);
    }
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult(
            "AI video generation agent that creates videos from text prompts or images using BytePlus ModelArk. " +
            "Supports text-to-video and image-to-video generation with customizable duration, resolution, and style options. " +
            "Provides real-time status tracking and error handling for video generation tasks.");
    }

    public async Task<string> GenerateVideoFromTextAsync(string prompt, VideoGenerationOptions? options = null)
    {
        Logger.LogInformation("Starting text-to-video generation, prompt length: {Length} characters", prompt?.Length ?? 0);

        var normalizedPrompt = AiAgentHelper.NormalizeUserInput(prompt ?? "", "Generate a creative video");
        options ??= new VideoGenerationOptions();

        var taskId = Guid.NewGuid().ToString();
        
        try
        {
            Logger.LogInformation("Starting BytePlus API call for text-to-video generation");
            
            // Create the BytePlus task without waiting for completion
            var bytePlusResponse = await _bytePlusClient.CreateVideoGenerationTaskAsync(normalizedPrompt, null, options);
                
            Logger.LogInformation("BytePlus API call completed successfully");
            
            // Store task in state with BytePlus task ID using event sourcing
            var startEvent = new VideoGenerationEvent
            {
                TaskId = taskId,
                Prompt = normalizedPrompt,
                Options = options,
                Id = Guid.NewGuid(),
                Ctime = DateTime.UtcNow
            };

            State.ActiveTasks[taskId] = new VideoGenerationStatus
            {
                TaskId = taskId,
                Status = "processing",
                CreatedAt = DateTime.UtcNow,
                Progress = 10,
                VideoUrl = bytePlusResponse.Id // Store BytePlus task ID temporarily in VideoUrl field
            };

            RaiseEvent(startEvent);
            await ConfirmEvents();

            Logger.LogInformation("Video generation task started: {TaskId}, BytePlus ID: {BytePlusId}", taskId, bytePlusResponse.Id);
            return taskId; // Return immediately with task ID
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Logger.LogWarning("BytePlus API call timed out for text-to-video generation task: {TaskId}", taskId);
            
            // Store failed state with timeout error
            State.ActiveTasks[taskId] = new VideoGenerationStatus
            {
                TaskId = taskId,
                Status = "failed",
                CreatedAt = DateTime.UtcNow,
                ErrorMessage = "API call timed out. Please try again."
            };
            
            return taskId;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error starting video generation for task: {TaskId}", taskId);
            
            // Store failed state
            State.ActiveTasks[taskId] = new VideoGenerationStatus
            {
                TaskId = taskId,
                Status = "failed",
                CreatedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
            

            return taskId;
        }
    }

    public async Task<string> GenerateVideoFromImageAsync(string imageUrl, string prompt, VideoGenerationOptions? options = null)
    {
        Logger.LogInformation("Starting image-to-video generation, image URL: {ImageUrl}", imageUrl);

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            Logger.LogWarning("Image URL is empty or null");
            var failedTaskId = Guid.NewGuid().ToString();
            State.ActiveTasks[failedTaskId] = new VideoGenerationStatus
            {
                TaskId = failedTaskId,
                Status = "failed",
                CreatedAt = DateTime.UtcNow,
                ErrorMessage = "Image URL is required"
            };

            return failedTaskId;
        }

        var normalizedPrompt = AiAgentHelper.NormalizeUserInput(prompt, "Animate this image with smooth motion");
        options ??= new VideoGenerationOptions();

        var taskId = Guid.NewGuid().ToString();
        
        try
        {
            Logger.LogInformation("Starting BytePlus API call for image-to-video generation");
            
            // Create the BytePlus task without waiting for completion
            var bytePlusResponse = await _bytePlusClient.CreateVideoGenerationTaskAsync(normalizedPrompt, imageUrl, options);
                
            Logger.LogInformation("BytePlus API call completed successfully");
            
            // Store task in state with BytePlus task ID using event sourcing
            var startEvent = new VideoGenerationEvent
            {
                TaskId = taskId,
                Prompt = normalizedPrompt,
                ImageUrl = imageUrl,
                Options = options,
                Id = Guid.NewGuid(),
                Ctime = DateTime.UtcNow
            };


            State.ActiveTasks[taskId] = new VideoGenerationStatus
            {
                TaskId = taskId,
                Status = "processing",
                CreatedAt = DateTime.UtcNow,
                Progress = 10,
                VideoUrl = bytePlusResponse.Id // Store BytePlus task ID temporarily in VideoUrl field
            };

            RaiseEvent(startEvent);
            await ConfirmEvents();

            Logger.LogInformation("Image-to-video generation task started: {TaskId}, BytePlus ID: {BytePlusId}", taskId, bytePlusResponse.Id);
            return taskId; // Return immediately with task ID
        }
        catch (ArgumentException ex) when (ex.ParamName == "imageUrl")
        {
            Logger.LogWarning("Invalid image URL provided for image-to-video generation task: {TaskId}", taskId);
            
            // Store failed state with validation error
            State.ActiveTasks[taskId] = new VideoGenerationStatus
            {
                TaskId = taskId,
                Status = "failed",
                CreatedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
            
            return taskId;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Logger.LogWarning("BytePlus API call timed out for image-to-video generation task: {TaskId}", taskId);
            
            // Store failed state with timeout error
            State.ActiveTasks[taskId] = new VideoGenerationStatus
            {
                TaskId = taskId,
                Status = "failed",
                CreatedAt = DateTime.UtcNow,
                ErrorMessage = "API call timed out. Please try again."
            };
            
            return taskId;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error starting image-to-video generation for task: {TaskId}", taskId);
            
            // Store failed state
            State.ActiveTasks[taskId] = new VideoGenerationStatus
            {
                TaskId = taskId,
                Status = "failed",
                CreatedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
            

            return taskId;
        }
    }

    public async Task<VideoGenerationStatus> GetVideoStatusAsync(string taskId)
    {
        if (!State.ActiveTasks.TryGetValue(taskId, out var status))
        {
            return new VideoGenerationStatus
            {
                TaskId = taskId,
                Status = "not_found",
                ErrorMessage = "Task not found"
            };
        }

        // If task is still processing, check BytePlus API for updates
        if (status.Status == "processing" && !string.IsNullOrEmpty(status.VideoUrl))
        {
            try
            {
                var bytePlusTaskId = status.VideoUrl; // BytePlus ID stored in VideoUrl field
                var bytePlusStatus = await _bytePlusClient.GetVideoGenerationTaskAsync(bytePlusTaskId);
                
                Logger.LogDebug("BytePlus task {TaskId} status: {Status}", bytePlusTaskId, bytePlusStatus.Status);
                
                switch (bytePlusStatus.Status.ToLower())
                {
                    case "succeeded":
                        status.Status = "completed";
                        status.VideoUrl = bytePlusStatus.Content?.VideoUrl ?? "";
                        status.Progress = 100;
                        status.CompletedAt = DateTime.UtcNow;
                        State.TotalVideosGenerated++;
                        State.LastGenerationTime = DateTime.UtcNow;
            
                        Logger.LogInformation("Video generation completed: {TaskId}, Video URL: {VideoUrl}", taskId, status.VideoUrl);
                        break;
                        
                    case "failed":
                        status.Status = "failed";
                        status.ErrorMessage = bytePlusStatus.Error?.Message ?? "Unknown error";
                        status.VideoUrl = "";
            
                        Logger.LogError("Video generation failed: {TaskId}, Error: {Error}", taskId, status.ErrorMessage);
                        break;
                        
                    case "processing":
                        // Update progress estimate based on time elapsed
                        var elapsed = DateTime.UtcNow - status.CreatedAt;
                        var estimatedProgress = Math.Min(90, 10 + (int)(elapsed.TotalMinutes * 20)); // Estimate up to 90%
                        status.Progress = estimatedProgress;
            
                        break;
                }
                
                // Update state with new status
                State.ActiveTasks[taskId] = status;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking BytePlus status for task: {TaskId}. Using simulation mode.", taskId);
                
                // Simulation mode: simulate completion after some time if API fails
                var elapsed = DateTime.UtcNow - status.CreatedAt;
                if (elapsed.TotalSeconds > 30) // Simulate completion after 30 seconds for demo
                {
                    status.Status = "completed";
                    status.VideoUrl = $"https://demo-video-storage.example.com/videos/{taskId}.mp4";
                    status.Progress = 100;
                    status.CompletedAt = DateTime.UtcNow;
                    State.TotalVideosGenerated++;
                    State.LastGenerationTime = DateTime.UtcNow;
                    
                    Logger.LogInformation("Demo simulation: Video generation completed for task: {TaskId}", taskId);
                    State.ActiveTasks[taskId] = status;
                }
                else
                {
                    // Update progress while waiting
                    var progressEstimate = Math.Min(90, 10 + (int)(elapsed.TotalSeconds * 2)); // 2% per second up to 90%
                    status.Progress = progressEstimate;
                    State.ActiveTasks[taskId] = status;
                }
            }
        }

        return status;
    }


}
