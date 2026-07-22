using System.Security.Claims;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Leaderboard;

namespace ltwnc.Tests.Services.Leaderboard;

public sealed class LeaderboardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetPageAsync_UsesPeriodAndPrivacyAndStableRanking()
    {
        await using var context = CreateContext();
        await SeedUserAsync(context, "user-a", "alice", isPublic: true, showStats: true);
        await SeedUserAsync(context, "user-b", "bob", isPublic: true, showStats: true);
        await SeedUserAsync(context, "user-private", "private", isPublic: false, showStats: true);
        await SeedUserAsync(context, "user-hidden", "hidden", isPublic: true, showStats: false);

        context.StudySessions.AddRange(
            Session("user-a", 100, Now.AddDays(-1)),
            Session("user-a", 50, Now.AddDays(-10)),
            Session("user-b", 120, Now.AddDays(-1)),
            Session("user-private", 999, Now.AddDays(-1)),
            Session("user-hidden", 999, Now.AddDays(-1)),
            Session("user-a", 500, Now.AddDays(-31)),
            new StudySession
            {
                UserId = "user-b",
                FlashcardSetId = 1,
                StartedAt = Now.AddMinutes(-10).UtcDateTime,
                CompletedAt = null,
                DurationSeconds = 500
            },
            new StudySession
            {
                UserId = "user-b",
                FlashcardSetId = 1,
                StartedAt = Now.AddMinutes(-10).UtcDateTime,
                CompletedAt = Now.AddMinutes(-5).UtcDateTime,
                DurationSeconds = null
            });
        await context.SaveChangesAsync();

        var service = new LeaderboardService(context, new FixedTimeProvider(Now));
        var result = await service.GetPageAsync(7, "user-a");

        Assert.Equal(7, result.PeriodDays);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("bob", result.Entries[0].Username);
        Assert.Equal(120, result.Entries[0].TotalSeconds);
        Assert.Equal("alice", result.Entries[1].Username);
        Assert.Equal(100, result.Entries[1].TotalSeconds);
        Assert.Same(result.Entries[1], result.ViewerEntry);
    }

    [Fact]
    public async Task GetPageAsync_ViewerOutsideTopTwentyGetsRankedEntry()
    {
        await using var context = CreateContext();
        for (var i = 1; i <= 21; i++)
        {
            string id = $"user-{i:00}";
            await SeedUserAsync(context, id, $"user{i:00}", isPublic: true, showStats: true);
            context.StudySessions.Add(Session(id, i == 21 ? 1 : 1000 - i, Now.AddDays(-1)));
        }

        await context.SaveChangesAsync();
        var service = new LeaderboardService(context, new FixedTimeProvider(Now));

        var result = await service.GetPageAsync(30, "user-21");

        Assert.Equal(30, result.PeriodDays);
        Assert.Equal(20, result.Entries.Count);
        Assert.NotNull(result.ViewerEntry);
        Assert.Equal(21, result.ViewerEntry!.Rank);
        Assert.DoesNotContain(result.Entries, entry => entry.UserId == "user-21");
    }

    private static StudySession Session(string userId, int durationSeconds, DateTimeOffset completedAt)
    {
        return new StudySession
        {
            UserId = userId,
            FlashcardSetId = 1,
            StartedAt = completedAt.AddSeconds(-durationSeconds).UtcDateTime,
            CompletedAt = completedAt.UtcDateTime,
            DurationSeconds = durationSeconds
        };
    }

    private static async Task SeedUserAsync(
        AppDbContext context,
        string userId,
        string username,
        bool isPublic,
        bool showStats)
    {
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = $"{username}@example.com",
            NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM"
        });
        context.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            IsPublic = isPublic,
            ShowStats = showStats
        });
        await context.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
