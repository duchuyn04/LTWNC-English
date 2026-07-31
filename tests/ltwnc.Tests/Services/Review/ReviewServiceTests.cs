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
        Assert.Empty(await context.ReviewProgresses.ToListAsync());
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

    private static ReviewService CreateService(AppDbContext context) =>
        new(context, new ReviewStateMachine(), new FixedTimeProvider(FixedNow));

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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
