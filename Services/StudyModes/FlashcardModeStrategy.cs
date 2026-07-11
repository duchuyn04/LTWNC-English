using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy cho chế độ học Flashcard: lật thẻ để ghi nhớ
public class FlashcardModeStrategy : IStudyModeStrategy
{
    private readonly IStudyCardQueryService _queryService;

    public FlashcardModeStrategy(IStudyCardQueryService queryService)
    {
        _queryService = queryService;
    }

    // Chế độ này phụ trách Flashcard
    public StudyMode Mode => StudyMode.Flashcard;

    // Lấy tất cả thẻ trong bộ đã qua bộ lọc chung, sắp xếp theo OrderIndex
    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        var query = _queryService.CreateFilteredQuery(setId, settings, userId);
        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
    }

    // Tạo thông tin hiển thị chế độ Flashcard trên Study Hub
    public StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings)
    {
        return new StudyModeOptionViewModel
        {
            Mode = StudyMode.Flashcard,
            Name = "Flashcard",
            Description = "Lật thẻ và ghi nhớ",
            IconClass = "ph-cards",
            ActionUrl = $"/Study/{setId}/Flashcard",
            IsAvailable = cards.Count > 0,
            CardCount = cards.Count,
            EstimatedSeconds = cards.Count * 15
        };
    }
}
