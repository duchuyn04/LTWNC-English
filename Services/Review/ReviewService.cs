using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
                value.UserId == userId
                && value.CompletedAtUtc == null
                && value.EndedAtUtc == null);

        return session == null ? null : await MapSessionAsync(session);
    }

    // Legacy multi-set start — không được ReviewController gọi. Xem IReviewService.
    public async Task<ReviewSessionViewModel?> StartAsync(string userId)
    {
        ReviewSessionViewModel? active = await GetActiveSessionAsync(userId);
        if (active != null)
        {
            return active;
        }

        List<Flashcard> cards = await _context.Flashcards
            .Include(value => value.FlashcardSet)
            .Where(value => value.FlashcardSet != null
                && value.FlashcardSet.UserId == userId)
            .OrderBy(value => value.FlashcardSetId)
            .ThenBy(value => value.OrderIndex)
            .ThenBy(value => value.Id)
            .ToListAsync();

        if (cards.Count == 0)
        {
            return null;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        UserStudySettings settings = await GetSettingsAsync(userId);
        int batchSize = ReviewSettingsPolicy.ValidateSessionSize(settings.ReviewSessionSize);
        DateTime reviewDate = GetVietnamDate(now);
        Dictionary<int, ReviewProgress> progressByCardId = await _context.ReviewProgresses
            .Where(value => value.UserId == userId && cards.Select(card => card.Id).Contains(value.FlashcardId))
            .ToDictionaryAsync(value => value.FlashcardId);
        List<Flashcard> dueCards = cards
            .Where(card => progressByCardId.TryGetValue(card.Id, out ReviewProgress? progress)
                && progress.NextReviewAtUtc != null
                && progress.NextReviewAtUtc <= now)
            .GroupBy(card => progressByCardId[card.Id].NextReviewAtUtc!.Value)
            .OrderBy(group => group.Key)
            .SelectMany(group => Shuffle(group))
            .Take(batchSize)
            .ToList();
        int newCardSlots = Math.Max(0, batchSize - dueCards.Count);
        List<NewCardAssignment> assignedNewCardsToday = await _context.ReviewSessionItems
            .Where(item => item.IsNewCardAtAssignment
                && item.ReviewSession != null
                && item.ReviewSession.UserId == userId
                && (item.NewCardAssignedDate == reviewDate || item.NewCardAssignedDate == null))
            .Select(item => new NewCardAssignment
            {
                FlashcardId = item.FlashcardId,
                SetId = item.Flashcard!.FlashcardSetId,
                AssignedDate = item.NewCardAssignedDate,
                SessionStartedAtUtc = item.ReviewSession!.StartedAtUtc
            })
            .ToListAsync();
        assignedNewCardsToday = assignedNewCardsToday
            .Where(value => value.AssignedDate == reviewDate
                || (value.AssignedDate == null
                    && GetVietnamDate(value.SessionStartedAtUtc) == reviewDate))
            .ToList();
        HashSet<int> reservedNewCardIds = assignedNewCardsToday
            .Select(value => value.FlashcardId)
            .ToHashSet();
        Dictionary<int, int> usedQuotaBySet = assignedNewCardsToday
            .GroupBy(value => value.SetId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.FlashcardId).Distinct().Count());

        Dictionary<int, List<Flashcard>> newCardsBySet = cards
            .Where(card => !progressByCardId.ContainsKey(card.Id)
                && !reservedNewCardIds.Contains(card.Id))
            .GroupBy(card => card.FlashcardSetId)
            .Select(group =>
            {
                FlashcardSet set = group.First().FlashcardSet!;
                int quota = ReviewSettingsPolicy.ValidateNewCardQuota(set.NewCardQuota);
                int remainingQuota = Math.Max(
                    0,
                    quota - usedQuotaBySet.GetValueOrDefault(set.Id));
                return new
                {
                    SetId = set.Id,
                    Cards = Shuffle(group).Take(remainingQuota).ToList()
                };
            })
            .Where(value => value.Cards.Count > 0)
            .ToDictionary(value => value.SetId, value => value.Cards);

        List<Flashcard> newCards = SelectNewCardsRoundRobin(newCardsBySet, newCardSlots);
        List<Flashcard> assignedCards = dueCards
            .Concat(newCards)
            .ToList();

        if (assignedCards.Count == 0)
        {
            return null;
        }

        ReviewSession session = new()
        {
            UserId = userId,
            StartedAtUtc = now,
            Items = assignedCards
                .Select((card, index) =>
                {
                    ReviewProgress? progress = progressByCardId.GetValueOrDefault(card.Id);
                    ReviewStage stage = progress?.Stage ?? ReviewStage.New;
                    return new ReviewSessionItem
                    {
                        FlashcardId = card.Id,
                        Flashcard = card,
                        OrderIndex = index,
                        IsNewCardAtAssignment = progress == null,
                        NewCardAssignedDate = progress == null ? reviewDate : null,
                        PreviousStage = stage,
                        NextStage = stage,
                        PreviousNextReviewAtUtc = progress?.NextReviewAtUtc,
                        NextReviewAtUtc = progress?.NextReviewAtUtc,
                        PreviousLongTermIntervalDays = progress?.LongTermIntervalDays ?? 0,
                        NextLongTermIntervalDays = progress?.LongTermIntervalDays ?? 0
                    };
                })
                .ToList()
        };

        _context.ReviewSessions.Add(session);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The active-session unique index is the final arbiter for two
            // near-simultaneous starts. Return the winner instead of leaking a
            // duplicate-session error; unrelated failures are rethrown.
            _context.ChangeTracker.Clear();
            ReviewSessionViewModel? activeAfterRace = await GetActiveSessionAsync(userId);
            if (activeAfterRace != null)
            {
                return activeAfterRace;
            }

            throw;
        }

        return await GetSessionAsync(session.Id, userId);
    }

    public async Task<ReviewSetViewModel?> GetSetAsync(string userId, int setId)
    {
        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == setId && value.UserId == userId);
        if (set == null)
        {
            return null;
        }

        ReviewSettingsViewModel settings = await GetOrCreateSetSettingsAsync(userId, set.Id);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<int> cardIds = await _context.Flashcards
            .Where(value => value.FlashcardSetId == set.Id)
            .Select(value => value.Id)
            .ToListAsync();
        Dictionary<int, ReviewProgress> progress = await _context.ReviewProgresses
            .AsNoTracking()
            .Where(value => value.UserId == userId && cardIds.Contains(value.FlashcardId))
            .ToDictionaryAsync(value => value.FlashcardId);

        return new ReviewSetViewModel
        {
            SetId = set.Id,
            SetTitle = set.Title,
            TotalCards = cardIds.Count,
            DueCards = cardIds.Count(id => progress.TryGetValue(id, out ReviewProgress? item)
                && item.NextReviewAtUtc != null
                && item.NextReviewAtUtc <= now),
            NewCards = cardIds.Count(id => !progress.ContainsKey(id)),
            Settings = settings
        };
    }

    public async Task<ReviewSessionViewModel?> StartAsync(string userId, int setId)
    {
        ReviewSessionViewModel? active = await GetActiveSessionAsync(userId);
        if (active != null)
        {
            return active;
        }

        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == setId && value.UserId == userId);
        if (set == null)
        {
            return null;
        }

        ReviewSettingsViewModel settings = await GetOrCreateSetSettingsAsync(userId, set.Id);
        int batchSize = ReviewSettingsPolicy.ValidateSessionSize(settings.ReviewSessionSize);
        int newCardQuota = ReviewSettingsPolicy.ValidateNewCardQuota(settings.NewCardQuota);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTime reviewDate = GetVietnamDate(now);
        List<Flashcard> cards = await _context.Flashcards
            .Include(value => value.FlashcardSet)
            .Where(value => value.FlashcardSetId == set.Id)
            .OrderBy(value => value.OrderIndex)
            .ThenBy(value => value.Id)
            .ToListAsync();
        if (cards.Count == 0)
        {
            return null;
        }

        List<int> cardIds = cards.Select(value => value.Id).ToList();
        Dictionary<int, ReviewProgress> progressByCardId = await _context.ReviewProgresses
            .Where(value => value.UserId == userId && cardIds.Contains(value.FlashcardId))
            .ToDictionaryAsync(value => value.FlashcardId);
        List<Flashcard> dueCards = cards
            .Where(card => progressByCardId.TryGetValue(card.Id, out ReviewProgress? progress)
                && progress.NextReviewAtUtc != null
                && progress.NextReviewAtUtc <= now)
            .OrderBy(card => progressByCardId[card.Id].NextReviewAtUtc)
            .ThenBy(card => card.OrderIndex)
            .ThenBy(card => card.Id)
            .Take(batchSize)
            .ToList();

        int usedNewCardQuota = await _context.ReviewSessionItems
            .Where(item => item.IsNewCardAtAssignment
                && cardIds.Contains(item.FlashcardId)
                && item.ReviewSession != null
                && item.ReviewSession.UserId == userId
                && item.NewCardAssignedDate == reviewDate)
            .Select(item => item.FlashcardId)
            .Distinct()
            .CountAsync();
        int newCardSlots = Math.Min(
            Math.Max(0, batchSize - dueCards.Count),
            Math.Max(0, newCardQuota - usedNewCardQuota));
        List<Flashcard> newCards = cards
            .Where(card => !progressByCardId.ContainsKey(card.Id))
            .OrderBy(card => card.OrderIndex)
            .ThenBy(card => card.Id)
            .Take(newCardSlots)
            .ToList();
        List<Flashcard> assignedCards = dueCards.Concat(newCards).ToList();
        if (assignedCards.Count == 0)
        {
            return null;
        }

        ReviewSession session = new()
        {
            UserId = userId,
            FlashcardSetId = set.Id,
            SettingsSnapshotJson = JsonSerializer.Serialize(settings),
            StartedAtUtc = now,
            Items = assignedCards.Select((card, index) =>
            {
                ReviewProgress? progress = progressByCardId.GetValueOrDefault(card.Id);
                ReviewStage stage = progress?.Stage ?? ReviewStage.New;
                return new ReviewSessionItem
                {
                    FlashcardId = card.Id,
                    Flashcard = card,
                    OrderIndex = index,
                    IsNewCardAtAssignment = progress == null,
                    NewCardAssignedDate = progress == null ? reviewDate : null,
                    PreviousStage = stage,
                    NextStage = stage,
                    PreviousNextReviewAtUtc = progress?.NextReviewAtUtc,
                    NextReviewAtUtc = progress?.NextReviewAtUtc,
                    PreviousLongTermIntervalDays = progress?.LongTermIntervalDays ?? 0,
                    NextLongTermIntervalDays = progress?.LongTermIntervalDays ?? 0
                };
            }).ToList()
        };

        _context.ReviewSessions.Add(session);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            ReviewSessionViewModel? activeAfterRace = await GetActiveSessionAsync(userId);
            if (activeAfterRace != null)
            {
                return activeAfterRace;
            }

            throw;
        }

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

        ReviewSessionItem? item = session.Items
            .SingleOrDefault(value => value.FlashcardId == flashcardId);
        if (item == null || item.Flashcard == null)
        {
            throw new KeyNotFoundException("Thẻ không thuộc lượt ôn.");
        }

        if (item.Rating != null)
        {
            return await BuildPersistedRatingResultAsync(userId, sessionId, flashcardId);
        }

        if (session.CompletedAtUtc != null || session.EndedAtUtc != null)
        {
            throw new InvalidOperationException("Lượt ôn đã kết thúc.");
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
        ReviewSettingsViewModel settings = await GetSessionSettingsAsync(session);
        int maximumIntervalDays = ReviewSettingsPolicy.ValidateMaxIntervalDays(settings.ReviewMaxIntervalDays);
        ReviewTransition transition = _stateMachine.Rate(current, rating, now, maximumIntervalDays);

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

        if (session.Items
            .Where(value => value.Flashcard != null)
            .All(value => value.Rating != null))
        {
            session.CompletedAtUtc = now;
        }
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have committed the first rating while this
            // request still held the original null RatedAtUtc value. Reload the
            // persisted item and return that result instead of applying a second
            // transition; unrelated persistence errors still bubble up.
            _context.ChangeTracker.Clear();
            ReviewSession? persistedSession = await QuerySessions()
                .SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId);
            ReviewSessionItem? persistedItem = persistedSession?.Items
                .SingleOrDefault(value => value.FlashcardId == flashcardId);
            if (persistedItem?.Rating != null)
            {
                return await BuildPersistedRatingResultAsync(userId, sessionId, flashcardId);
            }

            throw;
        }

        ReviewSessionViewModel updatedSession =
            (await GetSessionAsync(session.Id, userId))!;
        return new ReviewRatingResult
        {
            Session = updatedSession,
            Progress = new ReviewProgressViewModel
            {
                Stage = progress.Stage,
                NextReviewAtUtc = progress.NextReviewAtUtc!.Value,
                LongTermIntervalDays = progress.LongTermIntervalDays,
                LastRatedAtUtc = progress.LastRatedAtUtc!.Value
            }
        };
    }

    public async Task<ReviewSessionViewModel?> EndAsync(string userId, int sessionId)
    {
        ReviewSession? session = await QuerySessions()
            .SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId);
        if (session == null)
        {
            return null;
        }

        if (session.CompletedAtUtc == null && session.EndedAtUtc == null)
        {
            session.EndedAtUtc = _timeProvider.GetUtcNow();
            await _context.SaveChangesAsync();
        }

        return await GetSessionAsync(session.Id, userId);
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
        ReviewSettingsViewModel settings = await GetSessionSettingsAsync(session);
        int maximumIntervalDays = ReviewSettingsPolicy.ValidateMaxIntervalDays(settings.ReviewMaxIntervalDays);

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
                    Rating = item.Rating,
                    RatingPreviews = item.Rating == null
                        ? BuildRatingPreviews(progress, maximumIntervalDays)
                        : Array.Empty<ReviewRatingPreviewViewModel>(),
                    IsNewCard = item.IsNewCardAtAssignment,
                    IsRated = item.Rating != null
                };
            })
            .ToList();

        return new ReviewSessionViewModel
        {
            SessionId = session.Id,
            SetId = session.FlashcardSetId,
            SetTitle = cards.FirstOrDefault()?.SetTitle ?? string.Empty,
            TotalCards = cards.Count,
            RatedCards = cards.Count(card => card.IsRated),
            IsCompleted = session.CompletedAtUtc != null,
            IsEnded = session.EndedAtUtc != null,
            Settings = settings,
            Cards = cards
        };
    }

    private async Task<ReviewRatingResult> BuildPersistedRatingResultAsync(
        string userId,
        int sessionId,
        int flashcardId)
    {
        ReviewSession? session = await QuerySessions()
            .SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId);
        ReviewSessionItem? item = session?.Items
            .SingleOrDefault(value => value.FlashcardId == flashcardId);
        if (session == null || item?.Rating == null)
        {
            throw new InvalidOperationException("Không thể tải lại kết quả đánh giá đã lưu.");
        }

        ReviewProgress? progress = await _context.ReviewProgresses
            .AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.UserId == userId && value.FlashcardId == flashcardId);
        if (progress == null || progress.NextReviewAtUtc == null || progress.LastRatedAtUtc == null)
        {
            throw new InvalidOperationException("Lịch ôn của thẻ không còn nhất quán.");
        }

        return new ReviewRatingResult
        {
            Session = (await GetSessionAsync(sessionId, userId))!,
            Progress = new ReviewProgressViewModel
            {
                Stage = progress.Stage,
                NextReviewAtUtc = progress.NextReviewAtUtc.Value,
                LongTermIntervalDays = progress.LongTermIntervalDays,
                LastRatedAtUtc = progress.LastRatedAtUtc.Value
            }
        };
    }

    private IQueryable<ReviewSession> QuerySessions() => _context.ReviewSessions
        .Include(value => value.Items)
            .ThenInclude(value => value.Flashcard)
                .ThenInclude(value => value!.FlashcardSet);

    private async Task<UserStudySettings> GetSettingsAsync(string userId)
    {
        return await _context.UserStudySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == userId)
            ?? new UserStudySettings { UserId = userId };
    }

    private async Task<ReviewSettingsViewModel> GetOrCreateSetSettingsAsync(string userId, int setId)
    {
        ReviewSettings? settings = await _context.ReviewSettings
            .SingleOrDefaultAsync(value => value.UserId == userId && value.FlashcardSetId == setId);
        if (settings == null)
        {
            settings = ReviewSettings.CreateDefault(
                userId,
                setId,
                ReviewSettingsPolicy.DefaultNewCardQuota);
            _context.ReviewSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return ReviewSettingsMapper.ToViewModel(settings);
    }

    private async Task<ReviewSettingsViewModel> GetSessionSettingsAsync(ReviewSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.SettingsSnapshotJson))
        {
            ReviewSettingsViewModel? snapshot = JsonSerializer.Deserialize<ReviewSettingsViewModel>(
                session.SettingsSnapshotJson);
            if (snapshot != null)
            {
                return snapshot;
            }
        }

        // Tương thích cho phiên cũ chưa có snapshot; phiên mới không đi qua nhánh này.
        UserStudySettings legacy = await GetSettingsAsync(session.UserId);
        return new ReviewSettingsViewModel
        {
            ReviewSessionSize = legacy.ReviewSessionSize,
            ReviewMaxIntervalDays = legacy.ReviewMaxIntervalDays,
            ShowFrontTerm = legacy.ShowFrontTerm,
            ShowFrontDefinition = legacy.ShowFrontDefinition,
            ShowFrontIpa = legacy.ShowFrontIpa,
            ShowFrontImage = legacy.ShowFrontImage,
            ShowBackTerm = legacy.ShowBackTerm,
            ShowBackDefinition = legacy.ShowBackDefinition,
            ShowBackIpa = legacy.ShowBackIpa,
            ShowBackExample = legacy.ShowBackExample,
            ShowBackImage = legacy.ShowBackImage,
            HideImage = legacy.HideImage,
            BlurImage = legacy.BlurImage,
            LargeImage = legacy.LargeImage,
            PronounceFront = legacy.PronounceFront,
            PronounceBack = legacy.PronounceBack
        };
    }

    private List<ReviewRatingPreviewViewModel> BuildRatingPreviews(
        ReviewProgress? progress,
        int maximumIntervalDays)
    {
        ReviewSchedule current = progress == null
            ? new(ReviewStage.New, null, 0)
            : new(progress.Stage, progress.NextReviewAtUtc, progress.LongTermIntervalDays);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        return Enum.GetValues<ReviewRating>()
            .Select(rating =>
            {
                ReviewTransition transition = _stateMachine.Rate(
                    current,
                    rating,
                    now,
                    maximumIntervalDays);
                TimeSpan delay = transition.NextReviewAtUtc - now;
                return new ReviewRatingPreviewViewModel
                {
                    Rating = rating,
                    NextReviewAtUtc = transition.NextReviewAtUtc,
                    LongTermIntervalDays = transition.LongTermIntervalDays,
                    Delay = delay,
                    DelayLabel = FormatDelay(delay, transition.LongTermIntervalDays)
                };
            })
            .ToList();
    }

    private static string FormatDelay(TimeSpan delay, int longTermIntervalDays)
    {
        if (delay <= TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)Math.Ceiling(delay.TotalMinutes))} phút";
        }

        int days = Math.Max(1, (int)Math.Ceiling(delay.TotalDays));
        return $"{days} ngày";
    }

    private static IEnumerable<T> Shuffle<T>(IEnumerable<T> values)
    {
        List<T> list = values.ToList();
        for (int index = list.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Shared.Next(index + 1);
            (list[index], list[swapIndex]) = (list[swapIndex], list[index]);
        }

        return list;
    }

    private static List<Flashcard> SelectNewCardsRoundRobin(
        IReadOnlyDictionary<int, List<Flashcard>> cardsBySet,
        int maximumCount)
    {
        List<Flashcard> selected = new();
        if (maximumCount <= 0)
        {
            return selected;
        }

        Dictionary<int, int> offsets = cardsBySet.Keys.ToDictionary(key => key, _ => 0);
        while (selected.Count < maximumCount)
        {
            bool selectedInRound = false;
            foreach (int setId in cardsBySet.Keys.OrderBy(value => value))
            {
                List<Flashcard> cards = cardsBySet[setId];
                int offset = offsets[setId];
                if (offset >= cards.Count)
                {
                    continue;
                }

                selected.Add(cards[offset]);
                offsets[setId] = offset + 1;
                selectedInRound = true;
                if (selected.Count == maximumCount)
                {
                    break;
                }
            }

            if (!selectedInRound)
            {
                break;
            }
        }

        return selected;
    }

    private static DateTime GetVietnamDate(DateTimeOffset utcNow)
    {
        TimeZoneInfo vietnamTimeZone;
        try
        {
            vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }

        return TimeZoneInfo.ConvertTime(utcNow, vietnamTimeZone).Date;
    }

    private sealed class NewCardAssignment
    {
        public int FlashcardId { get; init; }

        public int SetId { get; init; }

        public DateTime? AssignedDate { get; init; }

        public DateTimeOffset SessionStartedAtUtc { get; init; }
    }
}
