using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Review;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Review;

// Covers the per-set overload ReviewService.StartAsync(userId, setId):
// set scope, ownership, queue ordering, daily quota,
// session resume and the immutable settings snapshot (issues 02 + 03).
public sealed class ReviewSetScopedServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 31, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_WithSetId_OnlySelectsCardsFromThatSet()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 3);
        await SeedSetAsync(context, setId: 2, firstCardId: 100, count: 2);
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1", 1))!;

        Assert.Equal(1, session.SetId);
        Assert.Equal(3, session.TotalCards);
        Assert.All(session.Cards, card => Assert.InRange(card.FlashcardId, 1, 3));
    }

    [Fact]
    public async Task StartAsync_WithForeignSet_ReturnsNull()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 3, owner: "user-1");
        ReviewService service = CreateService(context);

        ReviewSessionViewModel? session = await service.StartAsync("user-2", 1);

        Assert.Null(session);
        Assert.Empty(await context.ReviewSessions.ToListAsync());
    }

    [Fact]
    public async Task StartAsync_WithLegacyPausedSet_StillReturnsCards()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 3, reviewPaused: true);
        ReviewService service = CreateService(context);

        ReviewSessionViewModel? session = await service.StartAsync("user-1", 1);

        Assert.NotNull(session);
        Assert.Equal(3, session.TotalCards);
    }

    [Fact]
    public async Task StartAsync_WithSetId_PutsDueCardsBeforeNewCards()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 5);
        // Cards 1-3 are due; cards 4-5 stay new.
        for (int cardId = 1; cardId <= 3; cardId++)
        {
            context.ReviewProgresses.Add(new ReviewProgress
            {
                UserId = "user-1",
                FlashcardId = cardId,
                Stage = ReviewStage.Reviewing,
                NextReviewAtUtc = FixedNow.AddMinutes(-cardId),
                LongTermIntervalDays = 5
            });
        }
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1", 1))!;

        Assert.Equal(5, session.TotalCards);
        // First three slots are the due cards (ordered by due time), last two are new.
        Assert.Equal(new[] { 3, 2, 1 }, session.Cards.Take(3).Select(c => c.FlashcardId).ToArray());
        Assert.All(session.Cards.Take(3), card => Assert.False(card.IsNewCard));
        Assert.All(session.Cards.Skip(3), card => Assert.True(card.IsNewCard));
    }

    [Fact]
    public async Task StartAsync_WithSetId_LimitsNewCardsByPerSetQuota()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 6);
        // Per-set Review settings: large session but only 2 new cards per day.
        context.ReviewSettings.Add(new ReviewSettings
        {
            UserId = "user-1",
            FlashcardSetId = 1,
            ReviewSessionSize = 20,
            NewCardQuota = 2,
            ReviewMaxIntervalDays = 30
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1", 1))!;

        Assert.Equal(2, session.TotalCards);
        Assert.All(session.Cards, card => Assert.True(card.IsNewCard));
    }

    [Fact]
    public async Task StartAsync_WithSetId_RespectsSessionSizeOverDueCards()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 6);
        for (int cardId = 1; cardId <= 6; cardId++)
        {
            context.ReviewProgresses.Add(new ReviewProgress
            {
                UserId = "user-1",
                FlashcardId = cardId,
                Stage = ReviewStage.Reviewing,
                NextReviewAtUtc = FixedNow.AddMinutes(-cardId),
                LongTermIntervalDays = 5
            });
        }
        context.ReviewSettings.Add(new ReviewSettings
        {
            UserId = "user-1",
            FlashcardSetId = 1,
            ReviewSessionSize = 5,
            NewCardQuota = 5,
            ReviewMaxIntervalDays = 30
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1", 1))!;

        Assert.Equal(5, session.TotalCards);
    }

    [Fact]
    public async Task StartAsync_WithSetId_ResumesActiveSessionWithoutDuplicate()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 3);
        ReviewService service = CreateService(context);

        ReviewSessionViewModel first = (await service.StartAsync("user-1", 1))!;
        ReviewSessionViewModel second = (await service.StartAsync("user-1", 1))!;

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(1, await context.ReviewSessions.CountAsync());
    }

    [Fact]
    public async Task StartAsync_WithSetId_SnapshotsSettings_LaterEditsDoNotAffectRunningSession()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 2);
        context.ReviewSettings.Add(new ReviewSettings
        {
            UserId = "user-1",
            FlashcardSetId = 1,
            ReviewSessionSize = 20,
            NewCardQuota = 5,
            ReviewMaxIntervalDays = 30,
            ShowFrontTerm = false,
            BlurImage = true
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        ReviewSessionViewModel session = (await service.StartAsync("user-1", 1))!;
        Assert.False(session.Settings.ShowFrontTerm);
        Assert.True(session.Settings.BlurImage);

        // Edit the live per-set settings after the session started.
        ReviewSettings live = await context.ReviewSettings.SingleAsync(
            value => value.UserId == "user-1" && value.FlashcardSetId == 1);
        live.ShowFrontTerm = true;
        live.BlurImage = false;
        await context.SaveChangesAsync();

        ReviewSessionViewModel reloaded = (await service.GetSessionAsync(session.SessionId, "user-1"))!;

        // Running session still reads its immutable snapshot, not the edited row.
        Assert.False(reloaded.Settings.ShowFrontTerm);
        Assert.True(reloaded.Settings.BlurImage);
    }

    [Fact]
    public async Task StartAsync_WithSetId_EnforcesDailyNewCardQuotaAcrossSessions()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAsync(context, setId: 1, firstCardId: 1, count: 6);
        context.ReviewSettings.Add(new ReviewSettings
        {
            UserId = "user-1",
            FlashcardSetId = 1,
            ReviewSessionSize = 20,
            NewCardQuota = 2,
            ReviewMaxIntervalDays = 30
        });
        await context.SaveChangesAsync();
        ReviewService service = CreateService(context);

        // First session consumes the whole daily new-card quota (2 new cards).
        ReviewSessionViewModel first = (await service.StartAsync("user-1", 1))!;
        Assert.Equal(2, first.TotalCards);
        await service.EndAsync("user-1", first.SessionId);

        // Same day: no due cards and no quota left -> nothing to assign.
        ReviewSessionViewModel? second = await service.StartAsync("user-1", 1);

        Assert.Null(second);
    }

    private static ReviewService CreateService(AppDbContext context, TimeProvider? timeProvider = null) =>
        new(context, new ReviewStateMachine(), timeProvider ?? new FixedTimeProvider(FixedNow));

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedSetAsync(
        AppDbContext context,
        int setId,
        int firstCardId,
        int count,
        string owner = "user-1",
        bool reviewPaused = false)
    {
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = setId,
            UserId = owner,
            Title = $"Set {setId}",
            ReviewPaused = reviewPaused
        });
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
}
