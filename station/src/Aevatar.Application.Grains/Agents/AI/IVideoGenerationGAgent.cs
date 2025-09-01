using Aevatar.Core.Abstractions;
using Aevatar.GAgents.AIGAgent.Agent;

namespace Aevatar.Application.Grains.Agents.AI;

public interface IVideoGenerationGAgent : IAIGAgent, IStateGAgent<VideoGenerationState>
{
    Task<string> GenerateVideoFromTextAsync(string prompt, VideoGenerationOptions? options = null);
    Task<string> GenerateVideoFromImageAsync(string imageUrl, string prompt, VideoGenerationOptions? options = null);
    Task<VideoGenerationStatus> GetVideoStatusAsync(string taskId);
}

[GenerateSerializer]
public class VideoGenerationOptions
{
    [Id(0)] public int Duration { get; set; } = 5; // seconds
    [Id(1)] public string AspectRatio { get; set; } = "16:9";
    [Id(2)] public string Resolution { get; set; } = "1080p";
    [Id(3)] public int FrameRate { get; set; } = 24;
    [Id(4)] public string Style { get; set; } = "realistic";
    [Id(5)] public float MotionStrength { get; set; } = 0.7f;
    [Id(6)] public string MotionType { get; set; } = "smooth";
    [Id(7)] public float ImageInfluence { get; set; } = 0.8f;
}

[GenerateSerializer]
public class VideoGenerationStatus
{
    [Id(0)] public string TaskId { get; set; } = string.Empty;
    [Id(1)] public string Status { get; set; } = string.Empty; // pending, processing, completed, failed
    [Id(2)] public string VideoUrl { get; set; } = string.Empty;
    [Id(3)] public string ErrorMessage { get; set; } = string.Empty;
    [Id(4)] public int Progress { get; set; } = 0; // 0-100
    [Id(5)] public DateTime CreatedAt { get; set; }
    [Id(6)] public DateTime? CompletedAt { get; set; }
}
