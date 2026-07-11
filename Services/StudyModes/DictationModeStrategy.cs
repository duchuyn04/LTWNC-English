using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy cho chế độ học Dictation: phát âm và yêu cầu ngườidùng viết lại.
public class DictationModeStrategy : IStudyModeStrategy
{
    private readonly IStudyCardQueryService _queryService;

    public DictationModeStrategy(IStudyCardQueryService queryService)
    {
        _queryService = queryService;
    }

    // Strategy này chịu trách nhiệm duy nhất về StudyMode.Dictation
    public StudyMode Mode => StudyMode.Dictation;

    // Lấy danh sách thẻ cho bài Dictation:
    // 1. Dùng bộ lọc dùng chung (setId, sao, chưa thuộc)
    // 2. Nếu user chọn ExampleSentence mode thì thêm điều kiện: thẻ phải có câu ví dụ
    // 3. Vocabulary mode giữ lại cả thẻ không có câu ví dụ
    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        var query = _queryService.CreateFilteredQuery(setId, settings, userId);

        if (settings.DictationContentMode == DictationContentMode.ExampleSentence)
        {
            // ExampleSentence mode cần audio từ câu ví dụ nên loại thẻ thiếu câu ví dụ
            query = query.Where(f => !string.IsNullOrWhiteSpace(f.ExampleSentence));
        }

        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
    }

    // Tạo option hiển thị trên Study Hub cho chế độ Dictation
    public StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings)
    {
        var option = new StudyModeOptionViewModel
        {
            Mode = StudyMode.Dictation,
            Name = "Nghe chép",
            Description = "Nghe và viết lại từ",
            IconClass = "ph-headphones",
            ActionUrl = $"/Study/{setId}/Dictation",
            IsAvailable = cards.Count > 0,        // Có ít nhất 1 thẻ phù hợp
            CardCount = cards.Count,              // Số thẻ phù hợp sau lọc
            EstimatedSeconds = cards.Count * 25   // Ước tính 25 giây/thẻ
        };

        // Giải thích lý do không khả dụng theo đúng content mode đang chọn
        if (!option.IsAvailable)
        {
            option.UnavailableReason = settings.DictationContentMode == DictationContentMode.ExampleSentence
                ? "Không có thẻ có câu ví dụ phù hợp."
                : "Không có thẻ phù hợp với bộ lọc hiện tại.";
        }

        return option;
    }
}
