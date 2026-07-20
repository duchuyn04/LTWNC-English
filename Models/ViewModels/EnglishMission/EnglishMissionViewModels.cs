using ltwnc.Models.Entities;
using ltwnc.Services.EnglishMission;

namespace ltwnc.Models.ViewModels.EnglishMission;

public sealed class EnglishMissionTopicViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public IReadOnlyList<EnglishMissionTopic> Topics { get; set; } = [];
}

public sealed class EnglishMissionChatViewModel
{
    public int SetId { get; set; }
    public string SetTitle { get; set; } = string.Empty;
    public required Models.Entities.EnglishMission Mission { get; set; }
    public IReadOnlyList<EnglishMissionTargetWord> TargetWords { get; set; } = [];
    public IReadOnlyList<EnglishMissionTurn> Turns { get; set; } = [];
}
