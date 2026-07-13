using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy mode Flashcard: thẻ sau bộ lọc chung, option Study Hub.
public class FlashcardModeStrategy : IStudyModeStrategy
{
    // Lọc set / sao / chưa thuộc
    private readonly IStudyCardQueryService _queryService;

    // Inject query service dùng chung
    public FlashcardModeStrategy(IStudyCardQueryService queryService)
    {
        _queryService = queryService;
    }

    // Mode cố định Flashcard
    public StudyMode Mode => StudyMode.Flashcard;

    // Thẻ sau lọc chung, sort OrderIndex
    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        IQueryable<Flashcard> query = _queryService.CreateFilteredQuery(setId, settings, userId);

        List<Flashcard> cards = await query
            .OrderBy(flashcard => flashcard.OrderIndex)
            .ToListAsync();

        return cards;
    }

    // Option hub: URL Flashcard, ~15s/thẻ, available nếu còn thẻ
    public StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings)
    {
        bool isAvailable = cards.Count > 0;
        int cardCount = cards.Count;
        int estimatedSeconds = cardCount * 15;

        return new StudyModeOptionViewModel
        {
            Mode = StudyMode.Flashcard,
            Name = "Flashcard",
            Description = "Lật thẻ và ghi nhớ",
            IconClass = "ph-cards",
            ActionUrl = $"/Study/{setId}/Flashcard",
            IsAvailable = isAvailable,
            CardCount = cardCount,
            EstimatedSeconds = estimatedSeconds
        };
    }
}
