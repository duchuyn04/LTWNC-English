using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.AdminDashboard;
using ltwnc.Services.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.AdminDashboard;

public sealed class AdminDashboardKpiServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero));
    private readonly AdminDashboardKpiService _sut;

    public AdminDashboardKpiServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        _sut = new AdminDashboardKpiService(_context, _clock);
    }

    [Fact]
    public async Task GetSnapshotAsync_usesVietnamDayBoundaries()
    {
        FlashcardSet set = await SeedSetAsync("owner-vn-boundary");
        await SeedUserWithProfileAsync("before-boundary", "before@example.com", new DateTime(2026, 7, 12, 16, 59, 59, DateTimeKind.Utc));
        await SeedUserWithProfileAsync("inside-boundary", "inside@example.com", new DateTime(2026, 7, 12, 17, 0, 0, DateTimeKind.Utc));
        _context.StudySessions.AddRange(
            Session("before-boundary", set.Id, new DateTime(2026, 7, 12, 16, 59, 59, DateTimeKind.Utc), completed: true),
            Session("inside-boundary", set.Id, new DateTime(2026, 7, 12, 17, 0, 0, DateTimeKind.Utc), completed: true));
        await _context.SaveChangesAsync();

        AdminDashboardSnapshot snapshot = await _sut.GetSnapshotAsync(7);

        Assert.Equal(new DateTime(2026, 7, 12, 17, 0, 0, DateTimeKind.Utc), snapshot.Current.StartUtc);
        Assert.Equal(1, snapshot.CurrentMetrics.ActiveUsers);
        Assert.Equal(1, snapshot.CurrentMetrics.StudySessions);
        Assert.Equal(1, snapshot.CurrentMetrics.NewRegistrations);
    }

    [Fact]
    public async Task GetSnapshotAsync_countsDistinctActiveUsersAcrossLearningSources()
    {
        FlashcardSet set = await SeedSetAsync("owner-active");
        Flashcard card = await SeedCardAsync(set.Id);
        _context.StudySessions.AddRange(
            Session("same-user", set.Id, new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc), completed: true),
            Session("same-user", set.Id, new DateTime(2026, 7, 18, 2, 0, 0, DateTimeKind.Utc), completed: true));
        _context.UserProgresses.AddRange(
            Progress("same-user", card.Id, new DateTime(2026, 7, 18, 3, 0, 0, DateTimeKind.Utc)),
            Progress("other-user", card.Id, new DateTime(2026, 7, 18, 4, 0, 0, DateTimeKind.Utc)));
        await _context.SaveChangesAsync();

        AdminDashboardSnapshot snapshot = await _sut.GetSnapshotAsync(7);

        Assert.Equal(2, snapshot.CurrentMetrics.ActiveUsers);
    }

    [Fact]
    public async Task GetSnapshotAsync_excludesRecentActiveSessionsAndCountsOldIncompleteAsAbandoned()
    {
        FlashcardSet set = await SeedSetAsync("owner-completion");
        _context.StudySessions.AddRange(
            Session("done", set.Id, new DateTime(2026, 7, 18, 23, 0, 0, DateTimeKind.Utc), completed: true),
            Session("abandoned", set.Id, new DateTime(2026, 7, 18, 23, 20, 0, DateTimeKind.Utc), completed: false),
            Session("active", set.Id, new DateTime(2026, 7, 18, 23, 45, 0, DateTimeKind.Utc), completed: false));
        await _context.SaveChangesAsync();

        AdminDashboardSnapshot snapshot = await _sut.GetSnapshotAsync(7);

        Assert.Equal(2, snapshot.CurrentMetrics.CompletionRateDenominator);
        Assert.Equal(50m, snapshot.CurrentMetrics.CompletionRatePercent);
    }

    [Fact]
    public async Task GetSnapshotAsync_returnsInsufficientAiDataUntilMinimumSampleSize()
    {
        SeedAiLogs(new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc), total: 19, failures: 5);
        await _context.SaveChangesAsync();

        AdminDashboardSnapshot insufficient = await _sut.GetSnapshotAsync(7);

        Assert.Null(insufficient.CurrentMetrics.AiErrorRatePercent);
        Assert.Equal(19, insufficient.CurrentMetrics.AiSampleSize);

        SeedAiLogs(new DateTime(2026, 7, 18, 13, 0, 0, DateTimeKind.Utc), total: 1, failures: 0);
        await _context.SaveChangesAsync();

        AdminDashboardSnapshot enough = await _sut.GetSnapshotAsync(7);

        Assert.Equal(25m, enough.CurrentMetrics.AiErrorRatePercent);
        Assert.Equal(20, enough.CurrentMetrics.AiSampleSize);
    }

    [Fact]
    public async Task GetSnapshotAsync_comparesWithPreviousPeriod()
    {
        FlashcardSet set = await SeedSetAsync("owner-previous");
        _context.StudySessions.AddRange(
            Session("current-one", set.Id, new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc), completed: true),
            Session("current-two", set.Id, new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc), completed: true),
            Session("previous-one", set.Id, new DateTime(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc), completed: true));
        await _context.SaveChangesAsync();

        AdminDashboardSnapshot snapshot = await _sut.GetSnapshotAsync(7);

        Assert.Equal(2, snapshot.CurrentMetrics.StudySessions);
        Assert.Equal(1, snapshot.PreviousMetrics.StudySessions);
    }

    // Snapshot AJAX gom canh bao van hanh hien tai va khong can du lieu ca nhan.
    [Fact]
    public async Task GetLiveSnapshotAsync_ReturnsOperationalStatusAndActionableAlerts()
    {
        DateTime now = _clock.GetUtcNow().UtcDateTime;
        _context.AiProviders.Add(new AiProvider
        {
            Name = "Primary Gateway",
            AdapterType = "OpenAICompatible",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-live",
            IsEnabled = true,
            IsPrimary = true,
            LastCheckSucceeded = false,
            ConsecutiveFailureCount = 3
        });
        SeedAiLogs(now.AddMinutes(-2), total: 20, failures: 3);
        FlashcardSet alertSet = await SeedSetAsync("owner-alert-report");
        _context.ContentReports.Add(new ContentReport
        {
            FlashcardSetId = alertSet.Id,
            ReporterUserId = "private-user-id",
            Reason = "spam",
            Description = "noi dung rieng tu",
            Status = ContentReportStatus.Pending,
            CreatedAtUtc = now.AddHours(-25)
        });
        _context.AdminAuditLogs.Add(new AdminAuditLog
        {
            OccurredAtUtc = now.AddMinutes(-3),
            ActorUserId = "admin-id",
            ActorDisplay = "Admin",
            Action = AdminAuditActions.AchievementsResyncAll,
            Outcome = AdminAuditOutcome.Failure,
            TargetType = "AchievementCatalog",
            TargetId = "system"
        });
        await _context.SaveChangesAsync();

        AdminDashboardLiveSnapshot snapshot = await _sut.GetLiveSnapshotAsync(7);
        string[] alertCodes = snapshot.Alerts.Select(alert => alert.Code).ToArray();

        Assert.Equal(1, snapshot.AiStatus.TotalProviders);
        Assert.Equal(0, snapshot.AiStatus.ReadyProviders);
        Assert.Equal(15m, snapshot.AiStatus.ErrorRatePercent);
        Assert.Equal(1, snapshot.ContentReports.PendingCount);
        Assert.Equal(1, snapshot.ContentReports.OverdueCount);
        Assert.Contains("ai-primary-unstable", alertCodes);
        Assert.Contains("ai-error-rate", alertCodes);
        Assert.Contains("content-report-overdue", alertCodes);
        Assert.Contains("achievement-resync-failed", alertCodes);
    }

    // Canh bao suy ra tu trang thai moi nhat nen tu mat khi nguyen nhan da het.
    [Fact]
    public async Task GetLiveSnapshotAsync_RemovesAlertsWhenCurrentStateRecovers()
    {
        DateTime now = _clock.GetUtcNow().UtcDateTime;
        _context.AiProviders.Add(new AiProvider
        {
            Name = "Recovered Gateway",
            AdapterType = "OpenAICompatible",
            BaseUrl = "https://example.test/v1",
            ModelId = "model-live",
            IsEnabled = true,
            IsPrimary = true,
            LastCheckSucceeded = true,
            ConsecutiveFailureCount = 0
        });
        SeedAiLogs(now.AddMinutes(-2), total: 20, failures: 1);
        FlashcardSet recoveredSet = await SeedSetAsync("owner-recovered-report");
        _context.ContentReports.Add(new ContentReport
        {
            FlashcardSetId = recoveredSet.Id,
            ReporterUserId = "private-user-id",
            Reason = "spam",
            Status = ContentReportStatus.Dismissed,
            CreatedAtUtc = now.AddHours(-25),
            ResolvedAtUtc = now.AddMinutes(-10)
        });
        _context.AdminAuditLogs.AddRange(
            new AdminAuditLog
            {
                OccurredAtUtc = now.AddMinutes(-10),
                ActorUserId = "admin-id",
                ActorDisplay = "Admin",
                Action = AdminAuditActions.AchievementsResyncAll,
                Outcome = AdminAuditOutcome.Failure,
                TargetType = "AchievementCatalog",
                TargetId = "system"
            },
            new AdminAuditLog
            {
                OccurredAtUtc = now.AddMinutes(-1),
                ActorUserId = "admin-id",
                ActorDisplay = "Admin",
                Action = AdminAuditActions.AchievementsResyncAll,
                Outcome = AdminAuditOutcome.Success,
                TargetType = "AchievementCatalog",
                TargetId = "system"
            });
        await _context.SaveChangesAsync();

        AdminDashboardLiveSnapshot snapshot = await _sut.GetLiveSnapshotAsync(7);

        Assert.Empty(snapshot.Alerts);
        Assert.Equal(5m, snapshot.AiStatus.ErrorRatePercent);
        Assert.Equal(0, snapshot.ContentReports.PendingCount);
        Assert.Equal(0, snapshot.ContentReports.OverdueCount);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task SeedUserWithProfileAsync(string userId, string email, DateTime createdAtUtc)
    {
        _context.Users.Add(new IdentityUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant()
        });
        _context.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            CreatedAt = createdAtUtc,
            UpdatedAt = createdAtUtc
        });
        await _context.SaveChangesAsync();
    }

    private async Task<FlashcardSet> SeedSetAsync(string userId)
    {
        var set = new FlashcardSet
        {
            Title = $"Set {userId}",
            UserId = userId,
            IsPublic = true
        };
        _context.FlashcardSets.Add(set);
        await _context.SaveChangesAsync();
        return set;
    }

    private async Task<Flashcard> SeedCardAsync(int setId)
    {
        var card = new Flashcard
        {
            FlashcardSetId = setId,
            FrontText = "front",
            BackText = "back",
            Pronunciation = "/a/",
            PartOfSpeech = "noun",
            ExampleSentence = "example",
            ExampleMeaning = "ví dụ",
            OrderIndex = 0
        };
        _context.Flashcards.Add(card);
        await _context.SaveChangesAsync();
        return card;
    }

    private static StudySession Session(string userId, int setId, DateTime startedAtUtc, bool completed)
    {
        DateTime? completedAt = null;
        if (completed)
        {
            completedAt = startedAtUtc.AddMinutes(5);
        }

        return new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = StudyMode.Flashcard,
            StartedAt = startedAtUtc,
            CompletedAt = completedAt
        };
    }

    private static UserProgress Progress(string userId, int cardId, DateTime lastReviewedUtc)
    {
        return new UserProgress
        {
            UserId = userId,
            FlashcardId = cardId,
            LastReviewed = lastReviewedUtc
        };
    }

    private void SeedAiLogs(DateTime occurredAtUtc, int total, int failures)
    {
        for (int index = 0; index < total; index++)
        {
            bool isFailure = index < failures;
            string? failureKind = null;
            if (isFailure)
            {
                failureKind = "Timeout";
            }

            _context.AiOperationLogs.Add(new AiOperationLog
            {
                OccurredAtUtc = occurredAtUtc.AddSeconds(index),
                Operation = "Completion",
                Succeeded = !isFailure,
                FailureKind = failureKind,
                LatencyMs = 10
            });
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
