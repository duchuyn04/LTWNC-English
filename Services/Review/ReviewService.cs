using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Models.ViewModels.Study;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Review;

public sealed class ReviewService : IReviewService
{
    private readonly AppDbContext _context;
    private readonly ReviewStateMachine _stateMachine;
    private readonly TimeProvider _timeProvider;

    public ReviewService(
        AppDbContext context,
        ReviewStateMachine stateMachine,
        TimeProvider timeProvider)
    {
        _context = context;
        _stateMachine = stateMachine;
        _timeProvider = timeProvider;
    }

    public async Task<ReviewSessionViewModel?> GetActiveSessionAsync(string userId)
    {
        ReviewSession? session = await QuerySessions()
            .SingleOrDefaultAsync(value =>
                value.UserId == userId && value.CompletedAtUtc == null);

        return session == null ? null : await MapSessionAsync(session);
    }

    public async Task<ReviewSessionViewModel?> StartAsync(string userId)
    {
        ReviewSessionViewModel? active = await GetActiveSessionAsync(userId);
        if (active != null)
        {
            return active;
        }

        Flashcard? card = await _context.Flashcards
            .Include(value => value.FlashcardSet)
            .Where(value => value.FlashcardSet != null
                && value.FlashcardSet.UserId == userId)
            .Where(value => !_context.ReviewProgresses.Any(progress =>
                progress.UserId == userId && progress.FlashcardId == value.Id))
            .OrderBy(value => value.FlashcardSetId)
            .ThenBy(value => value.OrderIndex)
            .ThenBy(value => value.Id)
            .FirstOrDefaultAsync();

        if (card == null)
        {
            return null;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ReviewSession session = new()
        {
            UserId = userId,
            StartedAtUtc = now,
            Items =
            {
                new ReviewSessionItem
                {
                    FlashcardId = card.Id,
                    Flashcard = card,
                    OrderIndex = 0,
                    IsNewCardAtAssignment = true,
                    PreviousStage = ReviewStage.New,
                    NextStage = ReviewStage.New
                }
            }
        };

        _context.ReviewSessions.Add(session);
        await _context.SaveChangesAsync();

        return await GetSessionAsync(session.Id, userId);
    }

    public async Task<ReviewSessionViewModel?> GetSessionAsync(int sessionId, string userId)
    {
        ReviewSession? session = await QuerySessions()
            .SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId);

        return session == null ? null : await MapSessionAsync(session);
    }

    public async Task<ReviewRatingResult> RateAsync(
        string userId,
        int sessionId,
        int flashcardId,
        ReviewRating rating,
        bool answerRevealed)
    {
        if (!Enum.IsDefined(rating))
        {
            throw new ArgumentOutOfRangeException(nameof(rating));
        }

        if (!answerRevealed)
        {
            throw new InvalidOperationException("Bạn cần hiện đáp án trước khi chọn mức nhớ.");
        }

        ReviewSession? session = await QuerySessions()
            .SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId);

        if (session == null)
        {
            throw new KeyNotFoundException("Không tìm thấy lượt ôn.");
        }

        if (session.CompletedAtUtc != null)
        {
            throw new InvalidOperationException("Lượt ôn đã hoàn thành.");
        }

        ReviewSessionItem? item = session.Items
            .SingleOrDefault(value => value.FlashcardId == flashcardId);
        if (item == null || item.Flashcard == null)
        {
            throw new KeyNotFoundException("Thẻ không thuộc lượt ôn.");
        }

        if (item.Rating != null)
        {
            throw new InvalidOperationException("Thẻ này đã được đánh giá.");
        }

        ReviewProgress? progress = await _context.ReviewProgresses
            .SingleOrDefaultAsync(value =>
                value.UserId == userId && value.FlashcardId == flashcardId);

        ReviewSchedule current = progress == null
            ? new(ReviewStage.New, null, 0)
            : new(
                progress.Stage,
                progress.NextReviewAtUtc,
                progress.LongTermIntervalDays);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        ReviewTransition transition = _stateMachine.Rate(current, rating, now);

        if (progress == null)
        {
            progress = new ReviewProgress
            {
                UserId = userId,
                FlashcardId = flashcardId
            };
            _context.ReviewProgresses.Add(progress);
        }

        progress.Stage = transition.NextStage;
        progress.NextReviewAtUtc = transition.NextReviewAtUtc;
        progress.LongTermIntervalDays = transition.LongTermIntervalDays;
        progress.LastRatedAtUtc = now;

        item.Rating = rating;
        item.RatedAtUtc = now;
        item.PreviousStage = current.Stage;
        item.PreviousNextReviewAtUtc = current.NextReviewAtUtc;
        item.PreviousLongTermIntervalDays = current.LongTermIntervalDays;
        item.NextStage = transition.NextStage;
        item.NextReviewAtUtc = transition.NextReviewAtUtc;
        item.NextLongTermIntervalDays = transition.LongTermIntervalDays;

        // Ticket 01 phân đúng một thẻ, nên đánh giá xong là hoàn thành lượt.
        session.CompletedAtUtc = now;
        await _context.SaveChangesAsync();

        ReviewSessionViewModel completedSession =
            (await GetSessionAsync(session.Id, userId))!;
        return new ReviewRatingResult
        {
            Session = completedSession,
            Progress = new ReviewProgressViewModel
            {
                Stage = progress.Stage,
                NextReviewAtUtc = progress.NextReviewAtUtc!.Value,
                LongTermIntervalDays = progress.LongTermIntervalDays,
                LastRatedAtUtc = progress.LastRatedAtUtc!.Value
            }
        };
    }

    private async Task<ReviewSessionViewModel> MapSessionAsync(ReviewSession session)
    {
        List<int> cardIds = session.Items
            .Where(item => item.Flashcard != null)
            .Select(item => item.FlashcardId)
            .ToList();
        Dictionary<int, ReviewProgress> progressByCardId = await _context.ReviewProgresses
            .Where(value => value.UserId == session.UserId && cardIds.Contains(value.FlashcardId))
            .ToDictionaryAsync(value => value.FlashcardId);
        UserStudySettings settings = await _context.UserStudySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == session.UserId)
            ?? new UserStudySettings();

        List<ReviewCardViewModel> cards = session.Items
            .OrderBy(item => item.OrderIndex)
            .Where(item => item.Flashcard != null)
            .Select(item =>
            {
                ReviewProgress? progress = progressByCardId.GetValueOrDefault(item.FlashcardId);
                ReviewStage stage = item.Rating != null
                    ? item.NextStage
                    : progress?.Stage ?? ReviewStage.New;
                return new ReviewCardViewModel
                {
                    FlashcardId = item.FlashcardId,
                    SetTitle = item.Flashcard!.FlashcardSet?.Title ?? string.Empty,
                    FrontText = item.Flashcard.FrontText,
                    BackText = item.Flashcard.BackText,
                    Pronunciation = item.Flashcard.Pronunciation,
                    ExampleSentence = item.Flashcard.ExampleSentence,
                    ExampleMeaning = item.Flashcard.ExampleMeaning,
                    ImageUrl = item.Flashcard.ImageUrl,
                    UploadedImagePath = item.Flashcard.UploadedImagePath,
                    Stage = stage,
                    IsNewCard = item.IsNewCardAtAssignment,
                    IsRated = item.Rating != null
                };
            })
            .ToList();

        return new ReviewSessionViewModel
        {
            SessionId = session.Id,
            TotalCards = cards.Count,
            RatedCards = cards.Count(card => card.IsRated),
            IsCompleted = session.CompletedAtUtc != null,
            Settings = StudySettingsMapper.ToViewModel(settings),
            Cards = cards
        };
    }

    private IQueryable<ReviewSession> QuerySessions() => _context.ReviewSessions
        .Include(value => value.Items)
            .ThenInclude(value => value.Flashcard)
                .ThenInclude(value => value!.FlashcardSet);
}
