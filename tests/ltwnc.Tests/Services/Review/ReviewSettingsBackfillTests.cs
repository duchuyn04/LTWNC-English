using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Review;
using ltwnc.Services.Review;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Review;

// Application-level equivalent of the AddPerSetReviewSettings migration backfill.
// The migration's raw T-SQL (ISNULL / bracketed identifiers) cannot execute under
// EF InMemory, so these tests lock the same semantics the SQL implements:
// account-level settings copied into every owned set, each set keeping its own
// new-card quota, out-of-range legacy values clamped to defaults, and exactly one
// row per (user, set) with no duplicates.
public sealed class ReviewSettingsBackfillTests
{
    [Fact]
    public async Task GetOrCreateAsync_CopiesAccountSettingsIntoEveryOwnedSet_KeepingPerSetQuota()
    {
        await using AppDbContext context = CreateContext();
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ReviewSessionSize = 15,
            ReviewMaxIntervalDays = 90,
            ShowFrontTerm = false,
            ShowBackExample = false,
            BlurImage = true,
            PronounceBack = true
        });
        context.FlashcardSets.Add(new FlashcardSet { Id = 1, UserId = "user-1", Title = "A", NewCardQuota = 3 });
        context.FlashcardSets.Add(new FlashcardSet { Id = 2, UserId = "user-1", Title = "B", NewCardQuota = 7 });
        context.FlashcardSets.Add(new FlashcardSet { Id = 3, UserId = "user-1", Title = "C", NewCardQuota = 20 });
        await context.SaveChangesAsync();
        ReviewSettingsService service = new(context);

        ReviewSettingsViewModel set1 = (await service.GetOrCreateAsync("user-1", 1))!;
        ReviewSettingsViewModel set2 = (await service.GetOrCreateAsync("user-1", 2))!;
        ReviewSettingsViewModel set3 = (await service.GetOrCreateAsync("user-1", 3))!;

        // Account-level values propagated to all three rows.
        foreach (ReviewSettingsViewModel settings in new[] { set1, set2, set3 })
        {
            Assert.Equal(15, settings.ReviewSessionSize);
            Assert.Equal(90, settings.ReviewMaxIntervalDays);
            Assert.False(settings.ShowFrontTerm);
            Assert.False(settings.ShowBackExample);
            Assert.True(settings.BlurImage);
            Assert.True(settings.PronounceBack);
        }

        // Per-set new-card quota preserved individually.
        Assert.Equal(3, set1.NewCardQuota);
        Assert.Equal(7, set2.NewCardQuota);
        Assert.Equal(20, set3.NewCardQuota);

        // Exactly one row per set, no duplicates.
        Assert.Equal(3, await context.ReviewSettings.CountAsync());
        Assert.Equal(3, await context.ReviewSettings
            .Select(value => new { value.UserId, value.FlashcardSetId })
            .Distinct()
            .CountAsync());
    }

    [Fact]
    public async Task GetOrCreateAsync_ClampsOutOfRangeLegacyValuesToDefaults()
    {
        await using AppDbContext context = CreateContext();
        context.UserStudySettings.Add(new UserStudySettings
        {
            UserId = "user-1",
            ReviewSessionSize = 999,        // above 5-100 -> default 20
            ReviewMaxIntervalDays = 1       // below 30-365 -> default 30
        });
        context.FlashcardSets.Add(new FlashcardSet { Id = 1, UserId = "user-1", Title = "A", NewCardQuota = 42 }); // above 0-20 -> default 5
        await context.SaveChangesAsync();
        ReviewSettingsService service = new(context);

        ReviewSettingsViewModel settings = (await service.GetOrCreateAsync("user-1", 1))!;

        Assert.Equal(ReviewSettingsPolicy.DefaultSessionSize, settings.ReviewSessionSize);
        Assert.Equal(ReviewSettingsPolicy.DefaultMaxIntervalDays, settings.ReviewMaxIntervalDays);
        Assert.Equal(ReviewSettingsPolicy.DefaultNewCardQuota, settings.NewCardQuota);
    }

    [Fact]
    public async Task GetOrCreateAsync_WithoutAccountSettings_UsesDefaults()
    {
        await using AppDbContext context = CreateContext();
        context.FlashcardSets.Add(new FlashcardSet { Id = 1, UserId = "user-1", Title = "A", NewCardQuota = 8 });
        await context.SaveChangesAsync();
        ReviewSettingsService service = new(context);

        ReviewSettingsViewModel settings = (await service.GetOrCreateAsync("user-1", 1))!;

        Assert.Equal(ReviewSettingsPolicy.DefaultSessionSize, settings.ReviewSessionSize);
        Assert.Equal(ReviewSettingsPolicy.DefaultMaxIntervalDays, settings.ReviewMaxIntervalDays);
        Assert.Equal(8, settings.NewCardQuota);
    }

    [Fact]
    public async Task GetOrCreateAsync_RepeatedCalls_DoNotCreateDuplicateRows()
    {
        await using AppDbContext context = CreateContext();
        context.FlashcardSets.Add(new FlashcardSet { Id = 1, UserId = "user-1", Title = "A", NewCardQuota = 4 });
        await context.SaveChangesAsync();
        ReviewSettingsService service = new(context);

        await service.GetOrCreateAsync("user-1", 1);
        await service.GetOrCreateAsync("user-1", 1);
        await service.GetOrCreateAsync("user-1", 1);

        Assert.Equal(1, await context.ReviewSettings.CountAsync());
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
