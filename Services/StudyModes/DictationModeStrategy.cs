using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy mode Nghe chép: thêm lọc ExampleSentence nếu user chọn học theo câu ví dụ.
public class DictationModeStrategy : IStudyModeStrategy
{
    // Lọc set / sao / chưa thuộc
    private readonly IStudyCardQueryService _queryService;

    // Inject query service dùng chung
    public DictationModeStrategy(IStudyCardQueryService queryService)
    {
        // 1. Lưu dependency `_queryService` để các phương thức khác sử dụng.
        _queryService = queryService;
    }

    // Mode cố định Dictation
    public StudyMode Mode => StudyMode.Dictation;

    // Lọc chung; nếu content = ExampleSentence thì bắt buộc có câu ví dụ
    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        // 1. Gọi `CreateFilteredQuery` và lưu kết quả vào `query`.
        IQueryable<Flashcard> query = _queryService.CreateFilteredQuery(setId, settings, userId);

        // 2. Kiểm tra `settings.DictationContentMode == DictationContentMode.ExampleSentence` để chọn nhánh xử lý phù hợp.
        if (settings.DictationContentMode == DictationContentMode.ExampleSentence)
        {
            // Không có ExampleSentence thì TTS/câu đúng không dùng được
            // 3. Cập nhật `query` bằng giá trị mới.
            query = query.Where(flashcard => !string.IsNullOrWhiteSpace(flashcard.ExampleSentence));
        }

        // 4. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await query
            .OrderBy(flashcard => flashcard.OrderIndex)
            .ToListAsync();

        // 5. Trả `cards` cho nơi gọi.
        return cards;
    }

    // Option hub: URL Dictation, ~25s/thẻ; reason khi không có thẻ
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
        int estimatedSeconds = cardCount * 25;

        // 4. Khởi tạo `option` với dữ liệu ban đầu cần thiết.
        StudyModeOptionViewModel option = new StudyModeOptionViewModel
        {
            Mode = StudyMode.Dictation,
            Name = "Nghe chép",
            Description = "Nghe và viết lại từ",
            IconClass = "ph-headphones",
            ActionUrl = $"/Study/{setId}/Dictation",
            IsAvailable = isAvailable,
            CardCount = cardCount,
            EstimatedSeconds = estimatedSeconds
        };

        // 5. Kiểm tra `!isAvailable` để chọn nhánh xử lý phù hợp.
        if (!isAvailable)
        {
            // 6. Kiểm tra `settings.DictationContentMode == DictationContentMode.ExampleSentence` để chọn nhánh xử lý phù hợp.
            if (settings.DictationContentMode == DictationContentMode.ExampleSentence)
            {
                // 7. Cập nhật `option.UnavailableReason` bằng giá trị mới.
                option.UnavailableReason = "Không có thẻ có câu ví dụ phù hợp.";
            }
            else
            {
                // 8. Cập nhật `option.UnavailableReason` bằng giá trị mới.
                option.UnavailableReason = "Không có thẻ phù hợp với bộ lọc hiện tại.";
            }
        }

        // 9. Trả `option` cho nơi gọi.
        return option;
    }
}
