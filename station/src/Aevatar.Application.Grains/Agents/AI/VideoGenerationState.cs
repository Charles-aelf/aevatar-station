using Aevatar.GAgents.AIGAgent.State;

namespace Aevatar.Application.Grains.Agents.AI;

[GenerateSerializer]
public class VideoGenerationState : AIGAgentStateBase
{
    [Id(0)] public Dictionary<string, VideoGenerationStatus> ActiveTasks { get; set; } = new();
    [Id(1)] public int TotalVideosGenerated { get; set; } = 0;
    [Id(2)] public DateTime LastGenerationTime { get; set; }
}
