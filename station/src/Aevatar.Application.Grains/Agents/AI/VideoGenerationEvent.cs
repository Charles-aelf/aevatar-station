using Aevatar.Core.Abstractions;

namespace Aevatar.Application.Grains.Agents.AI;

[GenerateSerializer]
public class VideoGenerationEvent : StateLogEventBase<VideoGenerationEvent>
{
    [Id(0)] public string TaskId { get; set; } = string.Empty;
    [Id(1)] public string Prompt { get; set; } = string.Empty;
    [Id(2)] public string? ImageUrl { get; set; }
    [Id(3)] public VideoGenerationOptions? Options { get; set; }
}
