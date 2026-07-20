using ltwnc.Models.Entities;
using MissionEntity = ltwnc.Models.Entities.EnglishMission;

namespace ltwnc.Services.EnglishMission;

public sealed record EnglishMissionTopic(string Id, string Name, string Description);

public sealed class EnglishMissionStartResult
{
    public required MissionEntity Mission { get; init; }
    public required IReadOnlyList<EnglishMissionTargetWord> TargetWords { get; init; }
    public required IReadOnlyList<EnglishMissionTurn> Turns { get; init; }
}

public sealed class EnglishMissionRespondResult
{
    public required EnglishMissionTurn Turn { get; init; }
    public required MissionEntity Mission { get; init; }
    public required IReadOnlyList<EnglishMissionTargetWord> TargetWords { get; init; }
}

public interface IEnglishMissionService
{
    IReadOnlyList<EnglishMissionTopic> GetTopics();
    Task<EnglishMissionStartResult> StartAsync(string userId, int setId, string topic, CancellationToken cancellationToken = default);
    Task<EnglishMissionStartResult> GetAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken = default);
    Task<EnglishMissionRespondResult> RespondAsync(string userId, int setId, int sessionId, string clientTurnId, string userText, CancellationToken cancellationToken = default);
    Task CompleteAsync(string userId, int setId, int sessionId, CancellationToken cancellationToken = default);
}
