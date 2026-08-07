using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Review;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Review;

public sealed class ReviewServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 31, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_NewCard_CreatesOneCardReviewWithoutProgressRow()
    {
        await using AppDbContext context = CreateContext();
        Flashcard card = await SeedCardAsync(context);
        ReviewService service = CreateService(context);

        ReviewSessionViewModel? session = await service.StartAsync("user-1");

        Assert.NotNull(session);
        Assert.False(session.IsCompleted);
        Assert.Equal(1, session.TotalCards);
        Assert.Equal(0, session.RatedCards);
        Assert.Equal(card.Id, Assert.Single(session.Cards).FlashcardId);
        Assert.Equal("hello", session.Cards[0].FrontText);
        Assert.Equal("xin chào", session.Cards[0].BackText);
        Assert.Equal(4, session.Cards[0].RatingPreviews.Count);
        Assert.Equal(TimeSpan.FromMinutes(10),
            session.Cards[0].RatingPreviews.Single(value => value.Rating == ReviewRating.Again).Delay);
        Assert.Equal("hôm nay 15:40",
            session.Cards[0].RatingPreviews.Single(value => value.Rating == ReviewRating.Again).NextReviewLabel);
        Assert.Empty(await context.ReviewProgresses.ToListAsync());
    }

    [Fact]
    public async Task StartAsync_UsesConfiguredReviewBatchSize()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardsAsync(context, 7);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ReviewSessionSize = 5,
            ReviewMaxIntervalDays = 30
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        Assert.Equal(5, session.TotalCards);
    }

    [Fact]
    public async Task StartAsync_UsesConfiguredMaximumForRatingPreviews()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ReviewSessionSize = 20,
            ReviewMaxIntervalDays = 30
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        Assert.Equal(2, session.Cards[0].RatingPreviews
            .Single(value => value.Rating == ReviewRating.Good).LongTermIntervalDays);
        Assert.Equal(4, session.Cards[0].RatingPreviews
            .Single(value => value.Rating == ReviewRating.Easy).LongTermIntervalDays);
    }

    [Fact]
    public async Task StartAsync_RelearningHardPreviewShowsItsOneDayDelay()
    {
        await using AppDbContext context = CreateContext();
        Flashcard card = await SeedCardAsync(context);
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "user-1",
            FlashcardId = card.Id,
            Stage = ReviewStage.Relearning,
            NextReviewAtUtc = FixedNow.AddMinutes(-1),
            LongTermIntervalDays = 10
        });
        await context.SaveChangesAsync();
        ReviewSessionViewModel session = (await CreateService(context).StartAsync("user-1"))!;

        ReviewRatingPreviewViewModel preview = session.Cards[0].RatingPreviews
            .Single(value => value.Rating == ReviewRating.Hard);

        Assert.Equal(TimeSpan.FromDays(1), preview.Delay);
        Assert.Equal("1 ngày", preview.DelayLabel);
        Assert.Equal("ngày mai 15:30", preview.NextReviewLabel);
        Assert.Equal(10, preview.LongTermIntervalDays);
    }

    [Fact]
    public async Task StartAsync_UsesCurrentFlashcardDisplaySettings()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ShowFrontTerm = false,
            ShowFrontDefinition = true,
            ShowFrontIpa = false,
            ShowBackDefinition = false,
            ShowBackExample = false,
            HideImage = true
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        Assert.False(session.Settings.ShowFrontTerm);
        Assert.True(session.Settings.ShowFrontDefinition);
        Assert.False(session.Settings.ShowFrontIpa);
        Assert.False(session.Settings.ShowBackDefinition);
        Assert.False(session.Settings.ShowBackExample);
        Assert.True(session.Settings.HideImage);
    }

    [Fact]
    public async Task RateAsync_AnswerNotRevealed_RejectsRatingAndDoesNotCreateProgress()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RateAsync(
            "user-1",
            session.SessionId,
            session.Cards[0].FlashcardId,
            ReviewRating.Good,
            answerRevealed: false));

        Assert.Empty(await context.ReviewProgresses.ToListAsync());
        Assert.Null((await context.ReviewSessions.SingleAsync()).CompletedAtUtc);
    }

    [Theory]
    [InlineData(ReviewRating.Again, ReviewStage.Learning, 0, 10)]
    [InlineData(ReviewRating.Hard, ReviewStage.Learning, 0, 1440)]
    [InlineData(ReviewRating.Good, ReviewStage.Reviewing, 2, 2880)]
    [InlineData(ReviewRating.Easy, ReviewStage.Reviewing, 4, 5760)]
    public async Task RateAsync_NewStage_PersistsStateTransition(
        ReviewRating rating,
        ReviewStage expectedStage,
        int expectedLongTermDays,
        int expectedDelayMinutes)
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        ReviewRatingResult result = await service.RateAsync(
            "user-1",
            session.SessionId,
            session.Cards[0].FlashcardId,
            rating,
            answerRevealed: true);

        ReviewProgress progress = await context.ReviewProgresses.SingleAsync();
        ReviewSession persistedSession = await context.ReviewSessions
            .Include(value => value.Items)
            .SingleAsync();
        ReviewSessionItem item = Assert.Single(persistedSession.Items);

        Assert.Equal(expectedStage, progress.Stage);
        Assert.Equal(expectedLongTermDays, progress.LongTermIntervalDays);
        Assert.Equal(FixedNow.AddMinutes(expectedDelayMinutes), progress.NextReviewAtUtc);
        Assert.Equal(rating, item.Rating);
        Assert.Equal(rating, result.Session.Cards[0].Rating);
        Assert.Equal(expectedStage, item.NextStage);
        Assert.Equal(FixedNow, item.RatedAtUtc);
        Assert.NotNull(persistedSession.CompletedAtUtc);
        Assert.True(result.Session.IsCompleted);
    }

    [Fact]
    public async Task RateAsync_DoesNotChangeGeneralUserProgress()
    {
        await using AppDbContext context = CreateContext();
        Flashcard card = await SeedCardAsync(context);
        context.UserProgresses.Add(new UserProgress
        {
            UserId = "user-1",
            FlashcardId = card.Id,
            IsLearned = false,
            CorrectCount = 3,
            WrongCount = 2,
            Status = UserProgressStatus.Learning,
            LastReviewed = FixedNow.UtcDateTime
        });
        await context.SaveChangesAsync();

        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;
        await service.RateAsync(
            "user-1",
            session.SessionId,
            card.Id,
            ReviewRating.Good,
            answerRevealed: true);

        UserProgress progress = await context.UserProgresses.SingleAsync();
        Assert.False(progress.IsLearned);
        Assert.Equal(UserProgressStatus.Learning, progress.Status);
        Assert.Equal(3, progress.CorrectCount);
        Assert.Equal(2, progress.WrongCount);
        Assert.Equal(FixedNow.UtcDateTime, progress.LastReviewed);
    }

    [Fact]
    public async Task RateAsync_RespectsConfiguredMaximumIntervalForLongTermReview()
    {
        await using AppDbContext context = CreateContext();
        Flashcard card = await SeedCardAsync(context);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ReviewSessionSize = 20,
            ReviewMaxIntervalDays = 30
        });
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "user-1",
            FlashcardId = card.Id,
            Stage = ReviewStage.Reviewing,
            NextReviewAtUtc = FixedNow.AddDays(-1),
            LongTermIntervalDays = 20
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        ReviewRatingResult result = await service.RateAsync(
            "user-1", session.SessionId, card.Id, ReviewRating.Easy, answerRevealed: true);

        Assert.Equal(30, result.Progress.LongTermIntervalDays);
        Assert.Equal(FixedNow.AddDays(30), result.Progress.NextReviewAtUtc);
    }

    [Fact]
    public async Task RateAsync_ChangingMaximumIntervalOnlyAffectsLaterCalculations()
    {
        await using AppDbContext context = CreateContext();
        Flashcard card = await SeedCardAsync(context);
        UserStudySettings settings = new()
        {
            UserId = "user-1",
            ReviewSessionSize = 20,
            ReviewMaxIntervalDays = 30
        };
        context.UserStudySettings.Add(settings);
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "user-1",
            FlashcardId = card.Id,
            Stage = ReviewStage.Reviewing,
            NextReviewAtUtc = FixedNow.AddMinutes(-1),
            LongTermIntervalDays = 20
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel firstSession = (await service.StartAsync("user-1"))!;
        await service.RateAsync("user-1", firstSession.SessionId, card.Id, ReviewRating.Easy, true);
        ReviewProgress firstProgress = await context.ReviewProgresses.SingleAsync();
        Assert.Equal(30, firstProgress.LongTermIntervalDays);
        Assert.Equal(FixedNow.AddDays(30), firstProgress.NextReviewAtUtc);

        settings.ReviewMaxIntervalDays = 60;
        firstProgress.NextReviewAtUtc = FixedNow.AddMinutes(-1);
        await context.SaveChangesAsync();
        ReviewSessionViewModel secondSession = (await service.StartAsync("user-1"))!;
        await service.RateAsync("user-1", secondSession.SessionId, card.Id, ReviewRating.Easy, true);

        ReviewProgress secondProgress = await context.ReviewProgresses.SingleAsync();
        Assert.Equal(60, secondProgress.LongTermIntervalDays);
        Assert.Equal(FixedNow.AddDays(60), secondProgress.NextReviewAtUtc);
    }

    [Fact]
    public async Task StartAsync_WhenNoNewCardExists_ReturnsNull()
    {
        await using AppDbContext context = CreateContext();
        Flashcard card = await SeedCardAsync(context);
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "user-1",
            FlashcardId = card.Id,
            Stage = ReviewStage.Reviewing,
            LongTermIntervalDays = 2,
            NextReviewAtUtc = FixedNow.AddDays(2)
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel? result = await service.StartAsync("user-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task StartAsync_WhenActiveSessionExists_ReturnsTheSameSession()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);

        ReviewSessionViewModel first = (await service.StartAsync("user-1"))!;
        ReviewSessionViewModel second = (await service.StartAsync("user-1"))!;

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Single(await context.ReviewSessions.ToListAsync());
    }

    [Fact]
    public async Task StartAsync_WhenResumed_PreservesTheAssignedOrder()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardsAsync(context, 3);
        ReviewService service = CreateService(context);

        ReviewSessionViewModel first = (await service.StartAsync("user-1"))!;
        ReviewSessionViewModel resumed = (await service.StartAsync("user-1"))!;

        Assert.Equal(first.SessionId, resumed.SessionId);
        Assert.Equal(first.Cards.Select(card => card.FlashcardId), resumed.Cards.Select(card => card.FlashcardId));
        Assert.Equal(first.Cards.Select(card => card.IsNewCard), resumed.Cards.Select(card => card.IsNewCard));
    }

    [Fact]
    public async Task StartAsync_WhenLoadedFromAnotherContext_ResumesThePersistedBatch()
    {
        string databaseName = Guid.NewGuid().ToString();
        await using (AppDbContext seedContext = CreateContext(databaseName))
        {
            await SeedCardsAsync(seedContext, 3);
        }

        ReviewSessionViewModel first;
        await using (AppDbContext firstContext = CreateContext(databaseName))
        {
            first = (await CreateService(firstContext).StartAsync("user-1"))!;
        }

        await using AppDbContext resumedContext = CreateContext(databaseName);
        ReviewSessionViewModel resumed = (await CreateService(resumedContext).StartAsync("user-1"))!;

        Assert.Equal(first.SessionId, resumed.SessionId);
        Assert.Equal(first.Cards.Select(card => card.FlashcardId), resumed.Cards.Select(card => card.FlashcardId));
    }

    [Fact]
    public async Task StartAsync_PrioritizesDueCardsAndFillsWithNewCardsWithoutFutureCards()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardsAsync(context, 4);
        context.ReviewProgresses.AddRange(
            new ReviewProgress
            {
                UserId = "user-1",
                FlashcardId = 1,
                Stage = ReviewStage.Reviewing,
                NextReviewAtUtc = FixedNow.AddDays(-2),
                LongTermIntervalDays = 2
            },
            new ReviewProgress
            {
                UserId = "user-1",
                FlashcardId = 2,
                Stage = ReviewStage.Reviewing,
                NextReviewAtUtc = FixedNow.AddDays(-1),
                LongTermIntervalDays = 2
            },
            new ReviewProgress
            {
                UserId = "user-1",
                FlashcardId = 3,
                Stage = ReviewStage.Reviewing,
                NextReviewAtUtc = FixedNow.AddDays(1),
                LongTermIntervalDays = 2
            });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        Assert.Equal(new[] { 1, 2, 4 }, session.Cards.Select(card => card.FlashcardId));
        Assert.Equal(new[] { false, false, true }, session.Cards.Select(card => card.IsNewCard));
        Assert.Equal(3, session.TotalCards);
        Assert.Equal(3, (await context.ReviewSessions.Include(value => value.Items).SingleAsync()).Items.Count);
    }

    [Fact]
    public async Task RateAsync_MultiCardSession_CompletesOnlyAfterEveryAssignedCardIsRated()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardsAsync(context, 3);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        ReviewRatingResult first = await service.RateAsync(
            "user-1", session.SessionId, session.Cards[0].FlashcardId, ReviewRating.Again, true);
        Assert.False(first.Session.IsCompleted);
        Assert.Equal(1, first.Session.RatedCards);
        Assert.NotNull(await service.GetActiveSessionAsync("user-1"));

        await service.RateAsync("user-1", session.SessionId, session.Cards[1].FlashcardId, ReviewRating.Good, true);
        ReviewRatingResult last = await service.RateAsync(
            "user-1", session.SessionId, session.Cards[2].FlashcardId, ReviewRating.Easy, true);

        Assert.True(last.Session.IsCompleted);
        Assert.Equal(3, last.Session.RatedCards);
        Assert.Null(await service.GetActiveSessionAsync("user-1"));
        Assert.Equal(3, await context.ReviewSessionItems.CountAsync(item => item.Rating != null));
    }

    [Fact]
    public async Task RateAsync_AgainMarksCardHandledWithoutReinsertingItIntoCurrentSession()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardsAsync(context, 2);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        ReviewRatingResult result = await service.RateAsync(
            "user-1", session.SessionId, session.Cards[0].FlashcardId, ReviewRating.Again, true);

        Assert.Equal(2, result.Session.TotalCards);
        Assert.Equal(1, result.Session.RatedCards);
        Assert.DoesNotContain(
            session.Cards[0].FlashcardId,
            result.Session.Cards.Where(card => !card.IsRated).Select(card => card.FlashcardId));
        Assert.Equal(FixedNow.AddMinutes(10),
            (await context.ReviewProgresses.SingleAsync()).NextReviewAtUtc);
    }

    [Fact]
    public async Task EndAsync_EndsEarlyAndLeavesUnratedCardsUnchanged()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardsAsync(context, 3);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;
        await service.RateAsync("user-1", session.SessionId, session.Cards[0].FlashcardId, ReviewRating.Good, true);

        ReviewSessionViewModel ended = (await service.EndAsync("user-1", session.SessionId))!;

        Assert.True(ended.IsEnded);
        Assert.False(ended.IsCompleted);
        Assert.Equal(1, ended.RatedCards);
        Assert.Null(await service.GetActiveSessionAsync("user-1"));
        Assert.Equal(1, await context.ReviewProgresses.CountAsync());
        Assert.Equal(1, await context.ReviewSessionItems.CountAsync(item => item.Rating != null));
        Assert.Equal(2, await context.ReviewSessionItems.CountAsync(item => item.Rating == null));
    }

    [Fact]
    public async Task StartAsync_AppliesPerSetQuotaAndRoundRobinSelection()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetCardsAsync(context, 1, 1, 4, newCardQuota: 2);
        await SeedSetCardsAsync(context, 2, 101, 4, newCardQuota: 2);
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ReviewSessionSize = 20
        });
        await context.SaveChangesAsync();

        ReviewSessionViewModel session = (await CreateService(context).StartAsync("user-1"))!;
        ReviewSession persisted = await context.ReviewSessions
            .Include(value => value.Items)
            .ThenInclude(value => value.Flashcard)
            .SingleAsync();
        int[] selectedSetIds = persisted.Items
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.Flashcard!.FlashcardSetId)
            .ToArray();

        Assert.Equal(4, session.TotalCards);
        Assert.Equal(2, selectedSetIds.Count(id => id == 1));
        Assert.Equal(2, selectedSetIds.Count(id => id == 2));
        Assert.NotEqual(selectedSetIds[0], selectedSetIds[1]);
    }

    [Fact]
    public async Task StartAsync_QuotaZeroStillIncludesDueCardsButNoNewCards()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetCardsAsync(context, 1, 1, 2, newCardQuota: 0);
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "user-1",
            FlashcardId = 1,
            Stage = ReviewStage.Reviewing,
            NextReviewAtUtc = FixedNow.AddMinutes(-1),
            LongTermIntervalDays = 2
        });
        await context.SaveChangesAsync();

        ReviewSessionViewModel session = (await CreateService(context).StartAsync("user-1"))!;

        Assert.Equal(new[] { 1 }, session.Cards.Select(card => card.FlashcardId));
        Assert.False(session.Cards[0].IsNewCard);
    }

    [Fact]
    public async Task StartAsync_CountsLegacyDuplicateAssignmentsOncePerCard()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetCardsAsync(context, 1, 1, 2, newCardQuota: 2);
        Flashcard card = await context.Flashcards.SingleAsync(value => value.Id == 1);
        ReviewSession completedSession = new()
        {
            UserId = "user-1",
            StartedAtUtc = FixedNow,
            CompletedAtUtc = FixedNow
        };
        ReviewSession endedSession = new()
        {
            UserId = "user-1",
            StartedAtUtc = FixedNow,
            EndedAtUtc = FixedNow
        };
        completedSession.Items.Add(new ReviewSessionItem
        {
            Flashcard = card,
            OrderIndex = 0,
            IsNewCardAtAssignment = true
        });
        endedSession.Items.Add(new ReviewSessionItem
        {
            Flashcard = card,
            OrderIndex = 0,
            IsNewCardAtAssignment = true
        });
        context.ReviewSessions.AddRange(completedSession, endedSession);
        await context.SaveChangesAsync();

        ReviewSessionViewModel session = (await CreateService(context).StartAsync("user-1"))!;

        Assert.Equal(new[] { 2 }, session.Cards.Select(value => value.FlashcardId));
    }

    [Fact]
    public async Task StartAsync_IgnoresLegacyPausedFlag()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetCardsAsync(context, 1, 1, 2, newCardQuota: 5, reviewPaused: true);
        await SeedSetCardsAsync(context, 2, 101, 1, newCardQuota: 5);
        context.UserStudySettings.Add(new UserStudySettings { UserId = "user-1", ReviewSessionSize = 5 });
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "user-1",
            FlashcardId = 1,
            Stage = ReviewStage.Reviewing,
            NextReviewAtUtc = FixedNow.AddMinutes(-1),
            LongTermIntervalDays = 2
        });
        await context.SaveChangesAsync();

        ReviewSessionViewModel session = (await CreateService(context).StartAsync("user-1"))!;

        Assert.Contains(session.Cards, card => card.FlashcardId == 1);
    }

    [Fact]
    public async Task StartAsync_ReservesNewCardsForVietnameseDayAndResetsNextDay()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetCardsAsync(context, 1, 1, 2, newCardQuota: 5);
        await SeedSetCardsAsync(context, 2, 101, 5, newCardQuota: 5);
        context.UserStudySettings.Add(new UserStudySettings { UserId = "user-1", ReviewSessionSize = 5 });
        await context.SaveChangesAsync();
        MutableTimeProvider clock = new(FixedNow);
        ReviewService service = CreateService(context, clock);

        ReviewSessionViewModel first = (await service.StartAsync("user-1"))!;
        await service.EndAsync("user-1", first.SessionId);
        ReviewSessionViewModel second = (await service.StartAsync("user-1"))!;
        await service.EndAsync("user-1", second.SessionId);

        int[] firstIds = first.Cards.Select(card => card.FlashcardId).ToArray();
        int[] secondIds = second.Cards.Select(card => card.FlashcardId).ToArray();
        Assert.Empty(firstIds.Intersect(secondIds));
        Assert.Equal(2, secondIds.Length);

        clock.UtcNowValue = FixedNow.AddDays(1);
        ReviewSessionViewModel nextDay = (await service.StartAsync("user-1"))!;

        Assert.Equal(5, nextDay.TotalCards);
        Assert.Contains(nextDay.Cards, card => firstIds.Contains(card.FlashcardId));
    }

    [Fact]
    public async Task GetSessionAsync_MissingSession_ReturnsNull()
    {
        await using AppDbContext context = CreateContext();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel? actual = await service.GetSessionAsync(17, "user-1");

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetSessionAsync_ForeignSession_ReturnsNull()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        ReviewSessionViewModel? actual = await service.GetSessionAsync(session.SessionId, "user-2");

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetActiveSessionAsync_CompletedSession_ReturnsNull()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        await service.StartAsync("user-1");
        ReviewSession session = await context.ReviewSessions.SingleAsync();
        session.CompletedAtUtc = FixedNow;
        await context.SaveChangesAsync();

        ReviewSessionViewModel? actual = await service.GetActiveSessionAsync("user-1");

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetActiveSessionAsync_EndedSession_ReturnsNull()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        await service.StartAsync("user-1");
        ReviewSession session = await context.ReviewSessions.SingleAsync();
        session.EndedAtUtc = FixedNow;
        await context.SaveChangesAsync();

        ReviewSessionViewModel? actual = await service.GetActiveSessionAsync("user-1");

        Assert.Null(actual);
    }

    [Fact]
    public async Task RateAsync_InvalidRating_ThrowsArgumentOutOfRangeException()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RateAsync(
            "user-1",
            session.SessionId,
            session.Cards[0].FlashcardId,
            (ReviewRating)99,
            answerRevealed: true));

        Assert.Empty(await context.ReviewProgresses.ToListAsync());
    }

    [Fact]
    public async Task RateAsync_UnknownSession_ThrowsKeyNotFoundException()
    {
        await using AppDbContext context = CreateContext();
        ReviewService service = CreateService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RateAsync(
            "user-1", 17, 3, ReviewRating.Good, answerRevealed: true));
    }

    [Fact]
    public async Task RateAsync_CardOutsideSession_ThrowsKeyNotFoundException()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RateAsync(
            "user-1", session.SessionId, 999, ReviewRating.Good, answerRevealed: true));

        Assert.Empty(await context.ReviewProgresses.ToListAsync());
    }

    [Fact]
    public async Task RateAsync_EndedSession_RejectsUnratedCard()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;
        ReviewSession persistedSession = await context.ReviewSessions.SingleAsync();
        persistedSession.EndedAtUtc = FixedNow;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RateAsync(
            "user-1", session.SessionId, session.Cards[0].FlashcardId, ReviewRating.Good, true));

        Assert.Empty(await context.ReviewProgresses.ToListAsync());
    }

    [Fact]
    public async Task RateAsync_CompletedSession_RejectsUnratedCard()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardAsync(context);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;
        ReviewSession persistedSession = await context.ReviewSessions.SingleAsync();
        persistedSession.CompletedAtUtc = FixedNow;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RateAsync(
            "user-1", session.SessionId, session.Cards[0].FlashcardId, ReviewRating.Good, true));

        Assert.Empty(await context.ReviewProgresses.ToListAsync());
    }

    [Fact]
    public async Task EndAsync_MissingSession_ReturnsNull()
    {
        await using AppDbContext context = CreateContext();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel? actual = await service.EndAsync("user-1", 17);

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetSessionAsync_MapsRatedAndUnratedCardsWithCounts()
    {
        await using AppDbContext context = CreateContext();
        await SeedCardsAsync(context, 2);
        ReviewService service = CreateService(context);
        ReviewSessionViewModel session = (await service.StartAsync("user-1"))!;
        int ratedCardId = session.Cards[0].FlashcardId;

        await service.RateAsync("user-1", session.SessionId, ratedCardId, ReviewRating.Good, true);

        ReviewSessionViewModel actual = (await service.GetSessionAsync(session.SessionId, "user-1"))!;
        ReviewCardViewModel ratedCard = actual.Cards.Single(card => card.FlashcardId == ratedCardId);
        ReviewCardViewModel unratedCard = actual.Cards.Single(card => card.FlashcardId != ratedCardId);

        Assert.Equal(2, actual.TotalCards);
        Assert.Equal(1, actual.RatedCards);
        Assert.Equal(ReviewRating.Good, ratedCard.Rating);
        Assert.True(ratedCard.IsRated);
        Assert.Empty(ratedCard.RatingPreviews);
        Assert.Null(unratedCard.Rating);
        Assert.False(unratedCard.IsRated);
        Assert.Equal(4, unratedCard.RatingPreviews.Count);
    }

    private static ReviewService CreateService(AppDbContext context, TimeProvider? timeProvider = null) =>
        new(context, new ReviewStateMachine(), timeProvider ?? new FixedTimeProvider(FixedNow));

    private static AppDbContext CreateContext()
        => CreateContext(Guid.NewGuid().ToString());

    private static AppDbContext CreateContext(string databaseName)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Flashcard> SeedCardAsync(AppDbContext context)
    {
        FlashcardSet set = new()
        {
            Id = 1,
            UserId = "user-1",
            Title = "Everyday English"
        };
        Flashcard card = new()
        {
            Id = 1,
            FlashcardSetId = set.Id,
            FrontText = "hello",
            BackText = "xin chào",
            Pronunciation = "/həˈləʊ/",
            PartOfSpeech = "interjection",
            ExampleSentence = "Hello there!",
            ExampleMeaning = "Xin chào!",
            OrderIndex = 0
        };
        context.FlashcardSets.Add(set);
        context.Flashcards.Add(card);
        await context.SaveChangesAsync();
        return card;
    }

    private static async Task SeedCardsAsync(AppDbContext context, int count)
    {
        FlashcardSet set = new()
        {
            Id = 1,
            UserId = "user-1",
            Title = "Everyday English"
        };
        context.FlashcardSets.Add(set);
        for (int index = 1; index <= count; index++)
        {
            context.Flashcards.Add(new Flashcard
            {
                Id = index,
                FlashcardSetId = set.Id,
                FrontText = $"word-{index}",
                BackText = $"meaning-{index}",
                OrderIndex = index - 1
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedSetCardsAsync(
        AppDbContext context,
        int setId,
        int firstCardId,
        int count,
        int newCardQuota,
        bool reviewPaused = false)
    {
        FlashcardSet set = new()
        {
            Id = setId,
            UserId = "user-1",
            Title = $"Set {setId}",
            NewCardQuota = newCardQuota,
            ReviewPaused = reviewPaused
        };
        context.FlashcardSets.Add(set);
        for (int index = 0; index < count; index++)
        {
            context.Flashcards.Add(new Flashcard
            {
                Id = firstCardId + index,
                FlashcardSetId = setId,
                FrontText = $"word-{firstCardId + index}",
                BackText = $"meaning-{firstCardId + index}",
                OrderIndex = index
            });
        }

        await context.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public DateTimeOffset UtcNowValue { get; set; } = value;

        public override DateTimeOffset GetUtcNow() => UtcNowValue;
    }
}
