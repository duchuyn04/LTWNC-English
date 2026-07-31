using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Review;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Review;

public sealed class ReviewHardeningTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 31, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task RateAsync_RepeatedRequestReturnsFirstRatingWithoutRecomputingTransition()
    {
        string databaseName = Guid.NewGuid().ToString();
        ReviewSessionViewModel session;
        ReviewRatingResult first;
        await using (AppDbContext context = CreateContext(databaseName))
        {
            await SeedCardAsync(context);
            session = (await CreateService(context).StartAsync("user-1"))!;
            first = await CreateService(context).RateAsync(
                "user-1", session.SessionId, 1, ReviewRating.Good, answerRevealed: true);
        }

        await using AppDbContext retryContext = CreateContext(databaseName);
        ReviewRatingResult retry = await CreateService(retryContext).RateAsync(
            "user-1", session.SessionId, 1, ReviewRating.Easy, answerRevealed: true);

        Assert.Equal(ReviewRating.Good, first.Session.Cards[0].Rating);
        Assert.Equal(ReviewRating.Good, retry.Session.Cards[0].Rating);
        Assert.Equal(first.Progress.NextReviewAtUtc, retry.Progress.NextReviewAtUtc);
        Assert.Equal(ReviewRating.Good, (await retryContext.ReviewSessionItems.SingleAsync()).Rating);
        Assert.Equal(ReviewStage.Reviewing, (await retryContext.ReviewProgresses.SingleAsync()).Stage);
    }

    private static AppDbContext CreateContext(string? databaseName = null)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ReviewService CreateService(AppDbContext context) =>
        new(context, new ReviewStateMachine(), new FixedTimeProvider(FixedNow));

    private static async Task SeedCardAsync(AppDbContext context)
    {
        FlashcardSet set = new()
        {
            Id = 1,
            UserId = "user-1",
            Title = "Everyday English"
        };
        context.FlashcardSets.Add(set);
        context.Flashcards.Add(new Flashcard
        {
            Id = 1,
            FlashcardSetId = set.Id,
            FrontText = "hello",
            BackText = "xin chào",
            OrderIndex = 0
        });
        await context.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
