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
        _queryService = queryService;
        _questionFactory = questionFactory;
    }

    public StudyMode Mode => StudyMode.Quiz;

    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        return await _queryService.CreateFilteredQuery(setId, settings, userId)
            .OrderBy(card => card.OrderIndex)
            .ToListAsync();
    }

    public StudyModeOptionViewModel BuildOption(
        int setId,
        IReadOnlyList<Flashcard> cards,
        UserStudySettings settings)
    {
        bool isAvailable = cards.Count > 0;

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
        StudyModeOptionViewModel option = BuildOption(setId, cards, settings);
        if (!option.IsAvailable || string.IsNullOrWhiteSpace(userId))
        {
            return option;
        }

        QuizPoolAvailability availability = await _questionFactory.GetAvailabilityAsync(setId, userId);
        option.IsAvailable = availability.IsAvailable;
        option.UnavailableReason = availability.UnavailableReason;
        return option;
    }
}
