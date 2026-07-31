using ltwnc.Models.Entities;
using ltwnc.Data;
using ltwnc.Services.FlashcardSets;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace ltwnc.Tests.Services;

public sealed class FlashcardSetPolicyTests
{
    [Fact]
    public void NewCardQuotaPolicy_AllowsZeroThroughTwentyOnly()
    {
        Assert.Equal(0, ReviewSettingsPolicy.ValidateNewCardQuota(0));
        Assert.Equal(20, ReviewSettingsPolicy.ValidateNewCardQuota(20));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReviewSettingsPolicy.ValidateNewCardQuota(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReviewSettingsPolicy.ValidateNewCardQuota(21));
    }

    [Fact]
    public void Clone_UsesDefaultReviewPolicyInsteadOfCopyingSourcePolicy()
    {
        FlashcardSet source = new()
        {
            Title = "Source",
            NewCardQuota = 17,
            ReviewPaused = true,
            Flashcards = new[]
            {
                new Flashcard
                {
                    FrontText = "hello",
                    BackText = "xin chào",
                    OrderIndex = 0
                }
            }
        };

        FlashcardSet clone = source.Clone();

        Assert.Equal(ReviewSettingsPolicy.DefaultNewCardQuota, clone.NewCardQuota);
        Assert.False(clone.ReviewPaused);
        Assert.Equal(source.Title, clone.Title);
        Assert.NotSame(source.Flashcards.Single(), clone.Flashcards.Single());
    }

    [Fact]
    public async Task Service_PersistsQuotaAndPauseSettings()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using AppDbContext context = new(options);
        FlashcardSetService service = new(
            context,
            new Mock<IWebHostEnvironment>().Object);

        FlashcardSet set = await service.CreateSetAsync(
            "Travel",
            null,
            false,
            "user-1",
            newCardQuota: 12,
            reviewPaused: true);

        Assert.Equal(12, set.NewCardQuota);
        Assert.True(set.ReviewPaused);

        await service.UpdateSetAsync(
            set.Id,
            "Travel updated",
            null,
            false,
            "user-1",
            newCardQuota: 3,
            reviewPaused: false);

        FlashcardSet saved = await context.FlashcardSets.SingleAsync();
        Assert.Equal(3, saved.NewCardQuota);
        Assert.False(saved.ReviewPaused);
    }
}
