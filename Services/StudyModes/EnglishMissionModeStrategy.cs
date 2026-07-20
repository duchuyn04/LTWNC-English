using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

public sealed class EnglishMissionModeStrategy : IStudyModeStrategy
{
    private readonly IStudyCardQueryService _queryService;

    public EnglishMissionModeStrategy(IStudyCardQueryService queryService)
    {
        _queryService = queryService;
    }

    public StudyMode Mode => StudyMode.EnglishMission;

    public Task<List<Flashcard>> GetCardsAsync(int setId, UserStudySettings settings, string? userId) =>
        _queryService.CreateFilteredQuery(setId, settings, userId)
            .OrderBy(card => card.OrderIndex)
            .ToListAsync();

    public StudyModeOptionViewModel BuildOption(int setId, IReadOnlyList<Flashcard> cards, UserStudySettings settings)
    {
        bool available = cards.Count >= 3;
        return new StudyModeOptionViewModel
        {
            Mode = StudyMode.EnglishMission,
            Name = "English Mission",
            Description = "Dùng từ vựng trong hội thoại tình huống với gia sư AI",
            IconClass = "ph-chats-circle",
            ActionUrl = $"/Study/{setId}/Mission",
            IsAvailable = available,
            CardCount = Math.Min(cards.Count, 5),
            EstimatedSeconds = 5 * 60,
            UnavailableReason = available ? null : "Cần ít nhất 3 thẻ phù hợp."
        };
    }
}
