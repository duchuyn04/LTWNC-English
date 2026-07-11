using Microsoft.EntityFrameworkCore;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Services.StudyModes;

// Strategy cho chế độ học Dictation: nghe câu ví dụ và viết lại từ
public class DictationModeStrategy : IStudyModeStrategy
{
    private readonly IStudyCardQueryService _queryService;

    public DictationModeStrategy(IStudyCardQueryService queryService)
    {
        _queryService = queryService;
    }

    // Chế độ này phụ trách Dictation
    public StudyMode Mode => StudyMode.Dictation;

    // Lấy thẻ trong bộ đã qua bộ lọc chung, sau đó áp dụng quy tắc theo DictationContentMode
    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        var query = _queryService.CreateFilteredQuery(setId, settings, userId);

        // ExampleSentence mode cần thẻ có câu ví dụ; Vocabulary mode dùng cả thẻ không có ví dụ
        if (settings.DictationContentMode == DictationContentMode.ExampleSentence)
        {
            query = query.Where(f => !string.IsNullOrWhiteSpace(f.ExampleSentence));
        }

        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
    }

    // Tạo thông tin hiển thị chế độ Dictation trên Study Hub
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
            IsAvailable = cards.Count > 0,
            CardCount = cards.Count,
            EstimatedSeconds = cards.Count * 25
        };

        if (!option.IsAvailable)
        {
            option.UnavailableReason = settings.DictationContentMode == DictationContentMode.ExampleSentence
                ? "Không có thẻ có câu ví dụ phù hợp."
                : "Không có thẻ phù hợp với bộ lọc hiện tại.";
        }

        return option;
    }
}
