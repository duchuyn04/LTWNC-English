using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Profile;
using ltwnc.Services.Auth;
using ltwnc.Services.Profiles;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Profiles;

public sealed class ReviewActivityStatisticsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 1, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetPublicProfile_ReviewActivityUsesVietnameseDayAndCompletedSessionsOnly()
    {
        await using AppDbContext context = CreateContext();
        AppUser user = SeedUser(context);
        context.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            IsPublic = true,
            ShowStats = true,
            ShowActivity = true
        });

        FlashcardSet set = new()
        {
            Id = 1,
            UserId = user.Id,
            Title = "Everyday English",
            CreatedAt = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc),
            Flashcards =
            [
                new Flashcard { Id = 1, FrontText = "one", BackText = "một" },
                new Flashcard { Id = 2, FrontText = "two", BackText = "hai" },
                new Flashcard { Id = 3, FrontText = "three", BackText = "ba" }
            ]
        };
        context.FlashcardSets.Add(set);
        context.UserProgresses.Add(new UserProgress
        {
            UserId = user.Id,
            FlashcardId = 1,
            IsLearned = true
        });
        context.UserAchievements.Add(new UserAchievement
        {
            UserId = user.Id,
            Code = "review-test",
            Title = "Review test",
            Description = "Review test",
            UnlockedAt = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)
        });
        context.StudySessions.Add(new StudySession
        {
            UserId = user.Id,
            FlashcardSetId = set.Id,
            CompletedAt = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc)
        });

        ReviewSession completed = new()
        {
            UserId = user.Id,
            StartedAtUtc = new DateTimeOffset(2026, 7, 31, 16, 30, 0, TimeSpan.Zero),
            CompletedAtUtc = new DateTimeOffset(2026, 7, 31, 17, 5, 0, TimeSpan.Zero)
        };
        completed.Items.Add(new ReviewSessionItem
        {
            Flashcard = set.Flashcards.Single(card => card.Id == 1),
            OrderIndex = 0,
            Rating = ReviewRating.Good,
            RatedAtUtc = new DateTimeOffset(2026, 7, 31, 16, 59, 0, TimeSpan.Zero)
        });

        ReviewSession endedEarly = new()
        {
            UserId = user.Id,
            StartedAtUtc = new DateTimeOffset(2026, 7, 31, 17, 1, 0, TimeSpan.Zero),
            EndedAtUtc = new DateTimeOffset(2026, 7, 31, 17, 20, 0, TimeSpan.Zero)
        };
        endedEarly.Items.Add(new ReviewSessionItem
        {
            Flashcard = set.Flashcards.Single(card => card.Id == 2),
            OrderIndex = 0,
            Rating = ReviewRating.Again,
            RatedAtUtc = new DateTimeOffset(2026, 7, 31, 17, 1, 0, TimeSpan.Zero)
        });
        endedEarly.Items.Add(new ReviewSessionItem
        {
            Flashcard = set.Flashcards.Single(card => card.Id == 3),
            OrderIndex = 1
        });
        context.ReviewSessions.AddRange(completed, endedEarly);
        await context.SaveChangesAsync();

        ProfileService service = CreateService(context);

        PublicProfileViewModel result = (await service.GetPublicProfileAsync(
            user.UserName!,
            viewerUserId: null))!;

        Assert.NotNull(result.Statistics);
        Assert.Equal(2, result.Statistics.CompletedSessionCount);
        Assert.Equal(1, result.Statistics.LearnedFlashcardCount);
        Assert.Equal(1, result.Statistics.UnlockedBadgeCount);
        Assert.Equal(1, result.Statistics.CompletedReviewSessionCount);
        Assert.Equal(2, result.Statistics.ReviewActivityDayCount);
        Assert.Equal(2, result.Statistics.CurrentStreak);
        Assert.Contains(result.Timeline, item => item.Kind == "review");
    }

    [Fact]
    public async Task GetPublicProfile_MultipleReviewSessionsOnOneVietnameseDayCountOneActivityDay()
    {
        await using AppDbContext context = CreateContext();
        AppUser user = SeedUser(context);
        context.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            IsPublic = true,
            ShowStats = true
        });
        FlashcardSet set = new()
        {
            Id = 1,
            UserId = user.Id,
            Title = "Everyday English",
            CreatedAt = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc),
            Flashcards =
            [
                new Flashcard { Id = 1, FrontText = "one", BackText = "một" },
                new Flashcard { Id = 2, FrontText = "two", BackText = "hai" }
            ]
        };
        context.FlashcardSets.Add(set);
        for (int index = 0; index < 2; index++)
        {
            ReviewSession session = new()
            {
                UserId = user.Id,
                StartedAtUtc = new DateTimeOffset(2026, 7, 31, 8 + index, 0, 0, TimeSpan.Zero),
                CompletedAtUtc = new DateTimeOffset(2026, 7, 31, 8 + index, 5, 0, TimeSpan.Zero)
            };
            session.Items.Add(new ReviewSessionItem
            {
                Flashcard = set.Flashcards.Single(card => card.Id == index + 1),
                OrderIndex = 0,
                Rating = ReviewRating.Good,
                RatedAtUtc = new DateTimeOffset(2026, 7, 31, 8 + index, 1, 0, TimeSpan.Zero)
            });
            context.ReviewSessions.Add(session);
        }

        await context.SaveChangesAsync();

        ProfileService service = CreateService(context);
        PublicProfileViewModel result = (await service.GetPublicProfileAsync(
            user.UserName!,
            viewerUserId: null))!;
        ProfileStatisticsViewModel statistics = result.Statistics!;

        Assert.Equal(2, statistics.CompletedReviewSessionCount);
        Assert.Equal(1, statistics.ReviewActivityDayCount);
        Assert.Equal(1, statistics.CurrentStreak);
    }

    [Fact]
    public async Task GetPublicProfile_WithoutReviewActivity_PreservesUtcLegacyStreakCalendar()
    {
        await using AppDbContext context = CreateContext();
        AppUser user = SeedUser(context);
        context.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            IsPublic = true,
            ShowStats = true
        });
        context.StudySessions.AddRange(
            new StudySession
            {
                UserId = user.Id,
                CompletedAt = new DateTime(2026, 7, 31, 16, 30, 0, DateTimeKind.Utc)
            },
            new StudySession
            {
                UserId = user.Id,
                CompletedAt = new DateTime(2026, 7, 31, 17, 30, 0, DateTimeKind.Utc)
            });
        await context.SaveChangesAsync();

        ProfileService service = CreateService(context);
        PublicProfileViewModel result = (await service.GetPublicProfileAsync(
            user.UserName!,
            viewerUserId: null))!;

        Assert.Equal(2, result.Statistics!.CompletedSessionCount);
        Assert.Equal(0, result.Statistics.CompletedReviewSessionCount);
        Assert.Equal(1, result.Statistics.CurrentStreak);
    }

    private static AppDbContext CreateContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AppUser SeedUser(AppDbContext context)
    {
        AppUser user = new()
        {
            Id = "user-1",
            UserName = "reviewer",
            NormalizedUserName = "REVIEWER",
            Email = "reviewer@example.com",
            NormalizedEmail = "REVIEWER@EXAMPLE.COM"
        };
        context.AppUsers.Add(user);
        return user;
    }

    private static ProfileService CreateService(AppDbContext context) =>
        new(context, new Mock<IAuthService>().Object, new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
