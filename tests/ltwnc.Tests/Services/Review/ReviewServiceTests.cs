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

    private static ReviewService CreateService(AppDbContext context) =>
        new(context, new ReviewStateMachine(), new FixedTimeProvider(FixedNow));

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
