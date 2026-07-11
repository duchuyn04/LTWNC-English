using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy cho chế độ học Flashcard: hiển thị từng thẻ để ngườidùng lật và ghi nhớ.
public class FlashcardModeStrategy : IStudyModeStrategy
{
    private readonly IStudyCardQueryService _queryService;

    public FlashcardModeStrategy(IStudyCardQueryService queryService)
    {
        _queryService = queryService;
    }

    // Strategy này chịu trách nhiệm duy nhất về StudyMode.Flashcard
    public StudyMode Mode => StudyMode.Flashcard;

    // Lấy danh sách thẻ cho màn hình Flashcard:
    // 1. Dùng bộ lọc dùng chung (setId, sao, chưa thuộc)
    // 2. Sắp xếp đúng thứ tự hiển thị
    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        var query = _queryService.CreateFilteredQuery(setId, settings, userId);
        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
    }

    // Tạo option hiển thị trên Study Hub cho chế độ Flashcard
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
            IsAvailable = cards.Count > 0,       // Có thẻ mới cho học
            CardCount = cards.Count,             // Số thẻ sau khi lọc
            EstimatedSeconds = cards.Count * 15  // Ước tính 15 giây/thẻ
        };
    }
}
