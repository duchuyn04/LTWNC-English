using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Profiles;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace ltwnc.Tests.Services.Profiles;

public class ProfileServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Mock<UserManager<IdentityUser>> MockUserManager(IdentityUser user)
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        var manager = new Mock<UserManager<IdentityUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(item => item.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        return manager;
    }

    private static ProfileService CreateService(
        AppDbContext db,
        IdentityUser user,
        DateTimeOffset? now = null)
    {
        return new ProfileService(
            db,
            MockUserManager(user).Object,
            new FixedTimeProvider(now ?? Now));
    }

    [Fact]
    public async Task GetPublicProfile_PrivateProfile_ReturnsPrivateShellForNonOwner()
    {
        using AppDbContext db = CreateContext();
        var user = new IdentityUser { Id = "user-1", UserName = "private-user" };
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, IsPublic = false });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        PublicProfileViewModel? result = await service.GetPublicProfileAsync("private-user", "viewer");

        Assert.NotNull(result);
        Assert.True(result.IsPrivate);
        Assert.Null(result.Statistics);
        Assert.Empty(result.Timeline);
        Assert.Empty(result.Badges);
        Assert.Empty(result.PublicSets);
    }

    [Fact]
    public async Task GetPublicProfile_HiddenSections_AreNotReturned()
    {
        using AppDbContext db = CreateContext();
        var user = new IdentityUser { Id = "user-1", UserName = "user1" };
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, IsPublic = true });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", "viewer"))!;

        Assert.Null(result.Statistics);
        Assert.Empty(result.Timeline);
        Assert.Empty(result.Badges);
        Assert.Empty(result.PublicSets);
    }

    [Fact]
    public async Task GetPublicProfile_VisibleSections_ReturnCorrectCountsAndNewestTwentyEvents()
    {
        using AppDbContext db = CreateContext();
        var user = new IdentityUser { Id = "user-1", UserName = "user1" };
        db.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            IsPublic = true,
            ShowStats = true,
            ShowBadges = true,
            ShowActivity = true,
            ShowPublicSets = true
        });
        var set = new FlashcardSet
        {
            Id = 1,
            UserId = user.Id,
            Title = "Public set",
            IsPublic = true,
            CreatedAt = Now.UtcDateTime.AddDays(-1),
            Flashcards =
            [
                new Flashcard { Id = 1, FrontText = "one", BackText = "một" },
                new Flashcard { Id = 2, FrontText = "two", BackText = "hai" }
            ]
        };
        db.FlashcardSets.Add(set);
        db.UserProgresses.Add(new UserProgress
        {
            UserId = user.Id,
            FlashcardId = 1,
            IsLearned = true
        });
        db.UserAchievements.Add(new UserAchievement
        {
            UserId = user.Id,
            Code = "first",
            Title = "First badge",
            Description = "First badge",
            UnlockedAt = Now.UtcDateTime
        });
        for (int index = 0; index < 21; index++)
        {
            db.StudySessions.Add(new StudySession
            {
                UserId = user.Id,
                FlashcardSetId = set.Id,
                Mode = StudyMode.Dictation,
                Score = 80,
                CompletedAt = Now.UtcDateTime.AddMinutes(-index)
            });
        }

        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", "viewer"))!;

        Assert.NotNull(result.Statistics);
        Assert.Equal(1, result.Statistics.OwnedSetCount);
        Assert.Equal(1, result.Statistics.PublicSetCount);
        Assert.Equal(2, result.Statistics.TotalFlashcardCount);
        Assert.Equal(1, result.Statistics.LearnedFlashcardCount);
        Assert.Equal(21, result.Statistics.CompletedSessionCount);
        Assert.Equal(1, result.Statistics.UnlockedBadgeCount);
        Assert.Equal(2, result.Statistics.CurrentStreak);
        Assert.Equal(20, result.Timeline.Count);
        Assert.True(result.Timeline.SequenceEqual(
            result.Timeline.OrderByDescending(item => item.Timestamp)));
        Assert.Single(result.Badges);
        Assert.Single(result.PublicSets);
    }

    [Fact]
    public async Task GetPublicProfile_StreakStopsAtFirstUtcDateGap()
    {
        using AppDbContext db = CreateContext();
        var user = new IdentityUser { Id = "user-1", UserName = "user1" };
        db.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            IsPublic = true,
            ShowStats = true
        });
        db.FlashcardSets.AddRange(
            new FlashcardSet
            {
                Id = 1,
                UserId = user.Id,
                Title = "Today",
                IsPublic = true,
                CreatedAt = Now.UtcDateTime
            },
            new FlashcardSet
            {
                Id = 2,
                UserId = user.Id,
                Title = "Two days ago",
                IsPublic = true,
                CreatedAt = Now.UtcDateTime.AddDays(-2)
            });
        await db.SaveChangesAsync();
        ProfileService service = CreateService(db, user);

        PublicProfileViewModel result = (await service.GetPublicProfileAsync("user1", null))!;

        Assert.Equal(1, result.Statistics!.CurrentStreak);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
