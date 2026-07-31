using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.FlashcardSets;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.FlashcardSets;

public sealed class ReviewHardeningTests
{
    [Fact]
    public async Task DeleteCardAsync_RemovesReviewScheduleAndItemWithoutReplacingRemainingItems()
    {
        await using AppDbContext context = CreateContext();
        (FlashcardSet set, Flashcard removedCard, Flashcard remainingCard) = await SeedSetAsync(context);
        ReviewSession session = new()
        {
            UserId = "user-1",
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        session.Items.Add(new ReviewSessionItem
        {
            Flashcard = removedCard,
            OrderIndex = 0,
            IsNewCardAtAssignment = true
        });
        session.Items.Add(new ReviewSessionItem
        {
            Flashcard = remainingCard,
            OrderIndex = 1,
            IsNewCardAtAssignment = true
        });
        context.ReviewSessions.Add(session);
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "user-1",
            FlashcardId = removedCard.Id,
            Stage = ReviewStage.Reviewing,
            NextReviewAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        FlashcardSetService service = new(context, null!);
        await service.DeleteCardAsync(removedCard.Id, "user-1");

        Assert.Null(await context.Flashcards.FindAsync(removedCard.Id));
        Assert.Empty(await context.ReviewProgresses.ToListAsync());
        ReviewSession persisted = await context.ReviewSessions
            .Include(value => value.Items)
            .SingleAsync();
        Assert.Single(persisted.Items);
        Assert.Equal(remainingCard.Id, persisted.Items.Single().FlashcardId);
    }

    [Fact]
    public async Task DeleteCardAsync_PreservesCompletedSessionShellWhenItsLastItemIsDeleted()
    {
        await using AppDbContext context = CreateContext();
        (_, Flashcard removedCard, _) = await SeedSetAsync(context);
        ReviewSession session = new()
        {
            UserId = "user-1",
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            Items =
            [
                new ReviewSessionItem
                {
                    Flashcard = removedCard,
                    OrderIndex = 0,
                    Rating = ReviewRating.Good,
                    RatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
                }
            ]
        };
        context.ReviewSessions.Add(session);
        await context.SaveChangesAsync();

        FlashcardSetService service = new(context, null!);
        await service.DeleteCardAsync(removedCard.Id, "user-1");

        ReviewSession persisted = await context.ReviewSessions
            .Include(value => value.Items)
            .SingleAsync();
        Assert.NotNull(persisted.CompletedAtUtc);
        Assert.Empty(persisted.Items);
    }

    [Fact]
    public async Task DeleteSetAsync_RemovesAllReviewDataForItsCards()
    {
        await using AppDbContext context = CreateContext();
        (FlashcardSet set, Flashcard removedCard, Flashcard remainingCard) = await SeedSetAsync(context);
        ReviewSession session = new()
        {
            UserId = "user-1",
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        session.Items.Add(new ReviewSessionItem
        {
            Flashcard = removedCard,
            OrderIndex = 0,
            IsNewCardAtAssignment = true
        });
        session.Items.Add(new ReviewSessionItem
        {
            Flashcard = remainingCard,
            OrderIndex = 1,
            IsNewCardAtAssignment = true
        });
        context.ReviewSessions.Add(session);
        context.ReviewProgresses.AddRange(
            new ReviewProgress
            {
                UserId = "user-1",
                FlashcardId = removedCard.Id,
                Stage = ReviewStage.Reviewing,
                NextReviewAtUtc = DateTimeOffset.UtcNow
            },
            new ReviewProgress
            {
                UserId = "user-1",
                FlashcardId = remainingCard.Id,
                Stage = ReviewStage.Reviewing,
                NextReviewAtUtc = DateTimeOffset.UtcNow
            });
        await context.SaveChangesAsync();

        FlashcardSetService service = new(context, null!);
        await service.DeleteSetAsync(set.Id, "user-1");

        Assert.Null(await context.FlashcardSets.FindAsync(set.Id));
        Assert.Empty(await context.Flashcards.ToListAsync());
        Assert.Empty(await context.ReviewProgresses.ToListAsync());
        Assert.Empty(await context.ReviewSessionItems.ToListAsync());
        Assert.Empty(await context.ReviewSessions.ToListAsync());
    }

    [Fact]
    public async Task DeleteAllCardsAsync_RemovesReviewDataWithoutDeletingTheSet()
    {
        await using AppDbContext context = CreateContext();
        (FlashcardSet set, Flashcard removedCard, _) = await SeedSetAsync(context);
        ReviewSession session = new()
        {
            UserId = "user-1",
            StartedAtUtc = DateTimeOffset.UtcNow,
            Items =
            [
                new ReviewSessionItem
                {
                    Flashcard = removedCard,
                    OrderIndex = 0,
                    IsNewCardAtAssignment = true
                }
            ]
        };
        context.ReviewSessions.Add(session);
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "user-1",
            FlashcardId = removedCard.Id,
            Stage = ReviewStage.Reviewing,
            NextReviewAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        FlashcardSetService service = new(context, null!);
        await service.DeleteAllCardsAsync(set.Id, "user-1");

        Assert.NotNull(await context.FlashcardSets.FindAsync(set.Id));
        Assert.Empty(await context.Flashcards.ToListAsync());
        Assert.Empty(await context.ReviewProgresses.ToListAsync());
        Assert.Empty(await context.ReviewSessionItems.ToListAsync());
        Assert.Empty(await context.ReviewSessions.ToListAsync());
    }

    [Fact]
    public async Task CopyPublicSetAsync_ResetsReviewPolicyAndDoesNotCopyReviewProgress()
    {
        await using AppDbContext context = CreateContext();
        FlashcardSet source = new()
        {
            Id = 1,
            UserId = "author",
            Title = "Public source",
            IsPublic = true,
            NewCardQuota = 0,
            ReviewPaused = true
        };
        Flashcard sourceCard = new()
        {
            Id = 1,
            FlashcardSetId = source.Id,
            FrontText = "hello",
            BackText = "xin chào",
            OrderIndex = 0
        };
        context.FlashcardSets.Add(source);
        context.Flashcards.Add(sourceCard);
        context.ReviewProgresses.Add(new ReviewProgress
        {
            UserId = "author",
            FlashcardId = sourceCard.Id,
            Stage = ReviewStage.Reviewing,
            NextReviewAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        FlashcardSetService service = new(context, null!);
        FlashcardSet copy = await service.CopyPublicSetAsync(source.Id, "learner");

        Assert.False(copy.IsPublic);
        Assert.Equal(ReviewSettingsPolicy.DefaultNewCardQuota, copy.NewCardQuota);
        Assert.False(copy.ReviewPaused);
        Flashcard copiedCard = await context.Flashcards.SingleAsync(card => card.FlashcardSetId == copy.Id);
        Assert.Empty(await context.ReviewProgresses
            .Where(progress => progress.FlashcardId == copiedCard.Id)
            .ToListAsync());
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(builder => builder.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(FlashcardSet Set, Flashcard Removed, Flashcard Remaining)> SeedSetAsync(
        AppDbContext context)
    {
        FlashcardSet set = new()
        {
            Id = 1,
            UserId = "user-1",
            Title = "Everyday English"
        };
        Flashcard removed = new()
        {
            Id = 1,
            FlashcardSetId = set.Id,
            FrontText = "removed",
            BackText = "đã xóa",
            OrderIndex = 0
        };
        Flashcard remaining = new()
        {
            Id = 2,
            FlashcardSetId = set.Id,
            FrontText = "remaining",
            BackText = "còn lại",
            OrderIndex = 1
        };
        context.FlashcardSets.Add(set);
        context.Flashcards.AddRange(removed, remaining);
        await context.SaveChangesAsync();
        return (set, removed, remaining);
    }
}
