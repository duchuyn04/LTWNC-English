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
        IQueryable<Flashcard> query = _queryService.CreateFilteredQuery(setId, settings, userId);

        if (settings.DictationContentMode == DictationContentMode.ExampleSentence)
        {
            // Không có ExampleSentence thì TTS/câu đúng không dùng được
            query = query.Where(flashcard => !string.IsNullOrWhiteSpace(flashcard.ExampleSentence));
        }

        List<Flashcard> cards = await query
            .OrderBy(flashcard => flashcard.OrderIndex)
            .ToListAsync();

        return cards;
    }

    // Option hub: URL Dictation, ~25s/thẻ; reason khi không có thẻ
    public StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings)
    {
        bool isAvailable = cards.Count > 0;
        int cardCount = cards.Count;
        int estimatedSeconds = cardCount * 25;

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

        if (!isAvailable)
        {
            if (settings.DictationContentMode == DictationContentMode.ExampleSentence)
            {
                option.UnavailableReason = "Không có thẻ có câu ví dụ phù hợp.";
            }
            else
            {
                option.UnavailableReason = "Không có thẻ phù hợp với bộ lọc hiện tại.";
            }
        }

        return option;
    }
}
