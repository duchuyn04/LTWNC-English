using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using ltwnc.Services.Study;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.StudyModes;

public class QuizModeStrategy : IStudyModeStrategy
{
    private const string EmptyFilteredQuestionsReason =
        "Không có thẻ phù hợp với bộ lọc hiện tại.";

    private readonly IStudyCardQueryService _queryService;
    private readonly QuizQuestionFactory _questionFactory;

    public QuizModeStrategy(
        IStudyCardQueryService queryService,
        QuizQuestionFactory questionFactory)
    {
        // 1. Lưu dependency `_queryService` để các phương thức khác sử dụng.
        _queryService = queryService;
        // 2. Lưu dependency `_questionFactory` để các phương thức khác sử dụng.
        _questionFactory = questionFactory;
    }

    public StudyMode Mode => StudyMode.Quiz;

    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        // 1. Trả kết quả từ `ToListAsync` cho nơi gọi.
        return await _queryService.CreateFilteredQuery(setId, settings, userId)
            .OrderBy(card => card.OrderIndex)
            .ToListAsync();
    }

    public StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings)
    {
        // 1. Tính giá trị và lưu vào `isAvailable` để dùng ở bước tiếp theo.
        bool isAvailable = cards.Count > 0;

        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new StudyModeOptionViewModel
        {
            Mode = StudyMode.Quiz,
            Name = "Trắc nghiệm",
            Description = "Chọn đáp án đúng",
            IconClass = "ph-question",
            ActionUrl = $"/Study/{setId}/Quiz",
            IsAvailable = isAvailable,
            CardCount = cards.Count,
            EstimatedSeconds = cards.Count * 30,
            UnavailableReason = isAvailable ? null : EmptyFilteredQuestionsReason
        };
    }

    public async Task<StudyModeOptionViewModel> BuildOptionAsync(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings,
        string? userId)
    {
        // 1. Gọi `BuildOption` và lưu kết quả vào `option`.
        StudyModeOptionViewModel option = BuildOption(setId, cards, settings);
        // 2. Kiểm tra `!option.IsAvailable || string.IsNullOrWhiteSpace(userId)` để chọn nhánh xử lý phù hợp.
        if (!option.IsAvailable || string.IsNullOrWhiteSpace(userId))
        {
            // 3. Trả `option` cho nơi gọi.
            return option;
        }

        // 4. Gọi `GetAvailabilityAsync` và lưu kết quả vào `availability`.
        QuizPoolAvailability availability = await _questionFactory.GetAvailabilityAsync(setId, userId);
        // 5. Cập nhật `option.IsAvailable` bằng giá trị mới.
        option.IsAvailable = availability.IsAvailable;
        // 6. Cập nhật `option.UnavailableReason` bằng giá trị mới.
        option.UnavailableReason = availability.UnavailableReason;
        // 7. Trả `option` cho nơi gọi.
        return option;
    }
}
