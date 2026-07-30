using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.AdminDashboard;
using ltwnc.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.AdminDashboard;

public sealed class AdminDashboardServiceTests
{
    [Fact]
    public async Task GetAsync_DefaultsToSevenDaysAndCountsEssentialSignals()
    {
        await using AppDbContext context = CreateContext();
        var clock = new AdjustableTimeProvider();
        DateTime nowUtc = clock.GetUtcNow().UtcDateTime;
        context.StudySessions.AddRange(
            new StudySession { UserId = "u1", FlashcardSetId = 1, StartedAt = nowUtc.AddHours(-1), CompletedAt = nowUtc.AddMinutes(-40) },
            new StudySession { UserId = "u1", FlashcardSetId = 1, StartedAt = nowUtc.AddMinutes(-31) },
            new StudySession { UserId = "u1", FlashcardSetId = 1, StartedAt = nowUtc.AddMinutes(-10) });
        context.UserProfiles.Add(new UserProfile
        {
            UserId = "new-user",
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        });
        context.ContentReports.Add(new ContentReport
        {
            FlashcardSetId = 1,
            ReporterUserId = "u1",
            Reason = "spam",
            Status = ContentReportStatus.Pending,
            CreatedAtUtc = nowUtc
        });
        context.AiProviders.Add(new AiProvider
        {
            Name = "Primary",
            BaseUrl = "https://example.test/v1",
            ModelId = "model",
            IsPrimary = true,
            IsEnabled = true,
            LastCheckSucceeded = true,
            LastCheckedAt = nowUtc
        });
        await context.SaveChangesAsync();

        var service = new AdminDashboardService(context, clock);
        var result = await service.GetAsync(null, null);

        Assert.False(result.IsToday);
        Assert.Equal(new DateOnly(2026, 7, 13), result.From);
        Assert.Equal(new DateOnly(2026, 7, 19), result.To);
        Assert.Equal(1, result.PendingReportCount);
        Assert.True(result.AiStatus.IsHealthy);
        Assert.Equal(7, result.Activity.Count);
        Assert.Equal(1, result.Activity[^1].Completed);
        Assert.Equal(1, result.Activity[^1].Abandoned);
        Assert.Equal(7, result.NewUsers.Count);
        Assert.Equal(1, result.NewUsers[^1].Count);
        Assert.Equal(7, result.Reports.Count);
        Assert.Equal(1, result.Reports[^1].Count);
    }

    [Fact]
    public async Task GetAsync_RangeOverThirtyOneDaysFallsBackToToday()
    {
        await using AppDbContext context = CreateContext();
        var service = new AdminDashboardService(context, new AdjustableTimeProvider());

        var result = await service.GetAsync(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 7, 2));

        Assert.True(result.IsToday);
        Assert.Contains("31 ngày", result.RangeError);
        Assert.Single(result.Activity);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);
}
