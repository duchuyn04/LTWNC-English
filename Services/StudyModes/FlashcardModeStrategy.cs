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
        // 1. Lưu dependency `_queryService` để các phương thức khác sử dụng.
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
        // 1. Gọi `CreateFilteredQuery` và lưu kết quả vào `query`.
        IQueryable<Flashcard> query = _queryService.CreateFilteredQuery(setId, settings, userId);

        // 2. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await query
            .OrderBy(flashcard => flashcard.OrderIndex)
            .ToListAsync();

        // 3. Trả `cards` cho nơi gọi.
        return cards;
    }

    // Option hub: URL Flashcard, ~15s/thẻ, available nếu còn thẻ
    public StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings)
    {
        // 1. Tính giá trị và lưu vào `isAvailable` để dùng ở bước tiếp theo.
        bool isAvailable = cards.Count > 0;
        // 2. Tính giá trị và lưu vào `cardCount` để dùng ở bước tiếp theo.
        int cardCount = cards.Count;
        // 3. Tính giá trị và lưu vào `estimatedSeconds` để dùng ở bước tiếp theo.
        int estimatedSeconds = cardCount * 15;

        // 4. Tạo và trả đối tượng kết quả cho nơi gọi.
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
