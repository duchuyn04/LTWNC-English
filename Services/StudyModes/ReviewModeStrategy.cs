using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.StudyModes;

// Review là một mode độc lập theo bộ và không dùng bộ lọc Flashcard/Dictation.
public sealed class ReviewModeStrategy : IStudyModeStrategy
{
    private readonly AppDbContext _context;

    public ReviewModeStrategy(AppDbContext context)
    {
        _context = context;
    }

    public StudyMode Mode => StudyMode.Review;

    public async Task<List<Flashcard>> GetCardsAsync(
        int setId,
        UserStudySettings settings,
        string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<Flashcard>();
        }

        bool canReview = await _context.FlashcardSets
            .AnyAsync(set => set.Id == setId && set.UserId == userId);
        if (!canReview)
        {
            return new List<Flashcard>();
        }

        return await _context.Flashcards
            .Where(card => card.FlashcardSetId == setId)
            .OrderBy(card => card.OrderIndex)
            .ThenBy(card => card.Id)
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
            Mode = StudyMode.Review,
            Name = "Review",
            Description = "Ôn tập ngắt quãng theo bộ",
            IconClass = "ph-arrows-clockwise",
            ActionUrl = $"/Review/Set/{setId}",
            IsAvailable = isAvailable,
            CardCount = cards.Count,
            EstimatedSeconds = cards.Count * 20,
            UnavailableReason = isAvailable
                ? null
                : "Bộ chưa có thẻ phù hợp để ôn tập."
        };
    }
}
