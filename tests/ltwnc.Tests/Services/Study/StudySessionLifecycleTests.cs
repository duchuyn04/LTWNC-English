using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Study;

public sealed class StudySessionLifecycleTests
{
    [Fact]
    public async Task StartAndCompleteSession_ComputesServerDurationAndIsIdempotent()
    {
        await using var context = CreateContext();
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            UserId = "user-1",
            Title = "English",
            IsPublic = true
        });
        await context.SaveChangesAsync();

        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var service = new StudyService(
            context,
            [],
            new StudyModeStrategyResolver([]),
            TestStudyEvents.NoOpPublisher(),
            clock);

        var session = await service.StartSessionAsync("user-1", 1, StudyMode.Flashcard);
        clock.Advance(TimeSpan.FromHours(2));

        await service.CompleteSessionAsync("user-1", 1, session.Id);
        await service.CompleteSessionAsync("user-1", 1, session.Id);

        var saved = await context.StudySessions.SingleAsync();
        Assert.Equal(2 * 60 * 60, saved.DurationSeconds);
        Assert.NotNull(saved.CompletedAt);
    }

    [Fact]
    public async Task CompleteSession_ClampsDurationAndRejectsOtherUser()
    {
        await using var context = CreateContext();
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            UserId = "owner",
            Title = "English",
            IsPublic = true
        });
        await context.SaveChangesAsync();

        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var service = new StudyService(
            context,
            [],
            new StudyModeStrategyResolver([]),
            TestStudyEvents.NoOpPublisher(),
            clock);
        var session = await service.StartSessionAsync("owner", 1, StudyMode.Flashcard);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CompleteSessionAsync("intruder", 1, session.Id));

        clock.Advance(TimeSpan.FromHours(8));
        await service.CompleteSessionAsync("owner", 1, session.Id);

        var saved = await context.StudySessions.SingleAsync();
        Assert.Equal(4 * 60 * 60, saved.DurationSeconds);
    }

    [Fact]
    public async Task DictationCompletion_ComputesScoreOnServerAndRejectsFlashcardSession()
    {
        await using var context = CreateContext();
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var resolver = new StudyModeStrategyResolver([]);
        var service = new DictationService(
            context,
            resolver,
            TestStudyEvents.NoOpPublisher(),
            clock);

        var dictation = await service.CreateSessionAsync("user-1", 1, plannedItemCount: 2);
        context.DictationSessionDetails.AddRange(
            new DictationSessionDetail
            {
                StudySessionId = dictation.Id,
                FlashcardId = 1,
                IsCorrect = true
            },
            new DictationSessionDetail
            {
                StudySessionId = dictation.Id,
                FlashcardId = 2,
                IsCorrect = false
            });
        await context.SaveChangesAsync();
        clock.Advance(TimeSpan.FromMinutes(12));
        await service.CompleteSessionAsync(dictation.Id, 1, "user-1");

        var flashcard = new StudySession
        {
            UserId = "user-1",
            FlashcardSetId = 1,
            Mode = StudyMode.Flashcard,
            StartedAt = clock.GetUtcNow().UtcDateTime
        };
        context.StudySessions.Add(flashcard);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CompleteSessionAsync(flashcard.Id, 1, "user-1"));

        var saved = await context.StudySessions.SingleAsync(session => session.Id == dictation.Id);
        Assert.Equal(12 * 60, saved.DurationSeconds);
        Assert.Equal(50, saved.Score);
    }

    [Fact]
    public void DurationCalculation_ClampsNegativeElapsedTime()
    {
        DateTime completedAt = DateTime.UtcNow;

        Assert.Equal(
            0,
            StudySessionTiming.CalculateDurationSeconds(
                completedAt.AddMinutes(1),
                completedAt));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
