using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.CardActions;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.CardActions;

public sealed class DeleteCardsMementoTests
{
    [Fact]
    public async Task DeleteAndUndo_RestoresReviewAndExistingDependencies()
    {
        await using AppDbContext context = CreateContext();
        await SeedBaseAsync(context);
        AddReviewProgress(context, id: 10, cardId: 1);
        ReviewSession activeSession = CreateSession(
            id: 20,
            userId: "owner-1",
            startedAt: DateTimeOffset.Parse("2026-01-01T08:00:00+00:00"),
            settings: "{\"size\":2}",
            CreateReviewItem(100, 20, 1, 0),
            CreateReviewItem(101, 20, 2, 1));
        context.ReviewSessions.Add(activeSession);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        CardActionService service = CreateService(context);
        CardActionLog log = await service.ExecuteAsync(
            new DeleteCardsCommand(context, 1, "owner-1", [1]));

        context.ChangeTracker.Clear();
        Assert.Null(await context.Flashcards.FindAsync(1));
        Assert.Null(await context.UserProgresses.SingleOrDefaultAsync(row => row.FlashcardId == 1));
        Assert.Null(await context.ReviewProgresses.SingleOrDefaultAsync(row => row.FlashcardId == 1));
        Assert.Null(await context.DictationSessionDetails.SingleOrDefaultAsync(row => row.FlashcardId == 1));
        Assert.Null(await context.EnglishMissionTargetWords.SingleOrDefaultAsync(row => row.FlashcardId == 1));
        ReviewSession remainingSession = await context.ReviewSessions
            .Include(session => session.Items)
            .SingleAsync();
        Assert.Equal(20, remainingSession.Id);
        Assert.Equal(101, Assert.Single(remainingSession.Items).Id);

        await service.UndoAsync(log.Id, "owner-1");

        context.ChangeTracker.Clear();
        Flashcard restoredCard = await context.Flashcards.SingleAsync(card => card.Id == 1);
        Assert.Equal("hello", restoredCard.FrontText);
        Assert.Equal(1, (await context.UserProgresses.SingleAsync(row => row.FlashcardId == 1)).Id);
        ReviewProgress restoredProgress = await context.ReviewProgresses
            .SingleAsync(row => row.FlashcardId == 1);
        Assert.Equal(ReviewStage.Reviewing, restoredProgress.Stage);
        Assert.Equal(23, restoredProgress.LongTermIntervalDays);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-02T08:00:00+00:00"),
            restoredProgress.NextReviewAtUtc);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-01T08:00:00+00:00"),
            restoredProgress.LastRatedAtUtc);
        Assert.Single(await context.DictationSessionDetails.Where(row => row.FlashcardId == 1).ToListAsync());
        Assert.Single(await context.EnglishMissionTargetWords.Where(row => row.FlashcardId == 1).ToListAsync());

        ReviewSession restoredSession = await context.ReviewSessions
            .Include(session => session.Items)
            .SingleAsync();
        Assert.Equal("{\"size\":2}", restoredSession.SettingsSnapshotJson);
        Assert.Equal(new[] { 100, 101 }, restoredSession.Items
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.Id)
            .ToArray());
        ReviewSessionItem restoredItem = restoredSession.Items.Single(item => item.Id == 100);
        Assert.True(restoredItem.IsNewCardAtAssignment);
        Assert.Equal(DateTime.Parse("2026-01-01"), restoredItem.NewCardAssignedDate);
        Assert.Equal(ReviewRating.Good, restoredItem.Rating);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-01T08:30:00+00:00"),
            restoredItem.RatedAtUtc);
        Assert.Equal(ReviewStage.Learning, restoredItem.PreviousStage);
        Assert.Equal(ReviewStage.Reviewing, restoredItem.NextStage);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-01T08:00:00+00:00"),
            restoredItem.PreviousNextReviewAtUtc);
        Assert.Equal(
            DateTimeOffset.Parse("2026-01-02T08:00:00+00:00"),
            restoredItem.NextReviewAtUtc);
        Assert.Equal(5, restoredItem.PreviousLongTermIntervalDays);
        Assert.Equal(23, restoredItem.NextLongTermIntervalDays);
        Assert.NotNull((await context.CardActionLogs.SingleAsync()).UndoneAt);
    }

    [Fact]
    public async Task DeleteAndUndo_RestoresRemovedActiveSession()
    {
        await using AppDbContext context = CreateContext();
        await SeedBaseAsync(context);
        ReviewSession activeSession = CreateSession(
            id: 30,
            userId: "owner-1",
            startedAt: DateTimeOffset.Parse("2026-02-01T08:00:00+00:00"),
            settings: "{\"size\":1}",
            CreateReviewItem(110, 30, 1, 0));
        context.ReviewSessions.Add(activeSession);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        CardActionService service = CreateService(context);
        CardActionLog log = await service.ExecuteAsync(
            new DeleteCardsCommand(context, 1, "owner-1", [1]));

        Assert.Empty(await context.ReviewSessions.ToListAsync());
        Assert.Empty(await context.ReviewSessionItems.ToListAsync());

        await service.UndoAsync(log.Id, "owner-1");

        ReviewSession restoredSession = await context.ReviewSessions
            .Include(session => session.Items)
            .SingleAsync();
        Assert.Equal(30, restoredSession.Id);
        Assert.Equal("{\"size\":1}", restoredSession.SettingsSnapshotJson);
        Assert.Equal(110, Assert.Single(restoredSession.Items).Id);
    }

    [Fact]
    public async Task DeleteAndUndo_PreservesCompletedAndEndedSessionShells()
    {
        await using AppDbContext context = CreateContext();
        await SeedBaseAsync(context);
        DateTimeOffset completedAt = DateTimeOffset.Parse("2026-03-01T09:00:00+00:00");
        DateTimeOffset endedAt = DateTimeOffset.Parse("2026-03-02T09:00:00+00:00");
        ReviewSession completedSession = CreateSession(
            40,
            "owner-1",
            DateTimeOffset.Parse("2026-03-01T08:00:00+00:00"),
            "completed",
            CreateReviewItem(120, 40, 1, 0));
        completedSession.CompletedAtUtc = completedAt;
        ReviewSession endedSession = CreateSession(
            41,
            "owner-1",
            DateTimeOffset.Parse("2026-03-02T08:00:00+00:00"),
            "ended",
            CreateReviewItem(121, 41, 1, 0));
        endedSession.EndedAtUtc = endedAt;
        context.ReviewSessions.AddRange(completedSession, endedSession);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        CardActionService service = CreateService(context);
        CardActionLog log = await service.ExecuteAsync(
            new DeleteCardsCommand(context, 1, "owner-1", [1]));

        context.ChangeTracker.Clear();
        Assert.Equal(2, await context.ReviewSessions.CountAsync());
        Assert.Empty(await context.ReviewSessionItems.ToListAsync());
        Assert.Equal(completedAt, (await context.ReviewSessions.SingleAsync(session => session.Id == 40)).CompletedAtUtc);
        Assert.Equal(endedAt, (await context.ReviewSessions.SingleAsync(session => session.Id == 41)).EndedAtUtc);

        await service.UndoAsync(log.Id, "owner-1");

        Assert.Equal(new[] { 120, 121 }, (await context.ReviewSessionItems
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync()).ToArray());
        Assert.Equal(completedAt, (await context.ReviewSessions.SingleAsync(session => session.Id == 40)).CompletedAtUtc);
        Assert.Equal(endedAt, (await context.ReviewSessions.SingleAsync(session => session.Id == 41)).EndedAtUtc);
    }

    [Fact]
    public async Task UndoAsync_RejectsReviewSessionConflictBeforeRestoringAnything()
    {
        await using AppDbContext context = CreateContext();
        await SeedBaseAsync(context);
        AddReviewProgress(context, id: 11, cardId: 1);
        context.ReviewSessions.Add(CreateSession(
            id: 50,
            userId: "owner-1",
            startedAt: DateTimeOffset.Parse("2026-04-01T08:00:00+00:00"),
            settings: "original",
            CreateReviewItem(130, 50, 1, 0)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        CardActionService service = CreateService(context);
        CardActionLog log = await service.ExecuteAsync(
            new DeleteCardsCommand(context, 1, "owner-1", [1]));

        ReviewSession? deletedSession = await context.ReviewSessions.FindAsync(50);
        if (deletedSession != null)
        {
            context.ReviewSessions.Remove(deletedSession);
            await context.SaveChangesAsync();
        }
        context.ChangeTracker.Clear();
        context.ReviewSessions.Add(CreateSession(
            id: 50,
            userId: "owner-1",
            startedAt: DateTimeOffset.Parse("2026-04-02T08:00:00+00:00"),
            settings: "newer"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UndoAsync(log.Id, "owner-1"));

        Assert.Null(await context.Flashcards.FindAsync(1));
        Assert.Null(await context.ReviewProgresses.SingleOrDefaultAsync(row => row.Id == 11));
        ReviewSession conflict = await context.ReviewSessions.SingleAsync(session => session.Id == 50);
        Assert.Equal("newer", conflict.SettingsSnapshotJson);
        Assert.Null((await context.CardActionLogs.SingleAsync()).UndoneAt);
    }

    [Fact]
    public async Task UndoAsync_RejectsDuplicateReviewItemBeforeRestoringAnything()
    {
        await using AppDbContext context = CreateContext();
        await SeedBaseAsync(context);
        context.ReviewSessions.Add(CreateSession(
            id: 60,
            userId: "owner-1",
            startedAt: DateTimeOffset.Parse("2026-05-01T08:00:00+00:00"),
            settings: "duplicate",
            CreateReviewItem(140, 60, 1, 0)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        CardActionService service = CreateService(context);
        CardActionLog log = await service.ExecuteAsync(
            new DeleteCardsCommand(context, 1, "owner-1", [1]));
        DeleteCardsSnapshot state = JsonSerializer.Deserialize<DeleteCardsSnapshot>(log.SnapshotJson)!;
        state.Cards.Single().ReviewSessionItems.Add(state.Cards.Single().ReviewSessionItems.Single());
        log.SnapshotJson = JsonSerializer.Serialize(state);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UndoAsync(log.Id, "owner-1"));

        Assert.Null(await context.Flashcards.FindAsync(1));
        Assert.Empty(await context.ReviewSessions.ToListAsync());
        Assert.Null((await context.CardActionLogs.SingleAsync()).UndoneAt);
    }

    [Fact]
    public async Task UndoAsync_RejectsReviewItemWithWrongCardRelationship()
    {
        await using AppDbContext context = CreateContext();
        await SeedBaseAsync(context);
        context.ReviewSessions.Add(CreateSession(
            id: 61,
            userId: "owner-1",
            startedAt: DateTimeOffset.Parse("2026-06-01T08:00:00+00:00"),
            settings: "wrong-card",
            CreateReviewItem(141, 61, 1, 0)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        CardActionService service = CreateService(context);
        CardActionLog log = await service.ExecuteAsync(
            new DeleteCardsCommand(context, 1, "owner-1", [1]));
        DeleteCardsSnapshot state = JsonSerializer.Deserialize<DeleteCardsSnapshot>(log.SnapshotJson)!;
        state.Cards.Single().ReviewSessionItems.Single().FlashcardId = 2;
        log.SnapshotJson = JsonSerializer.Serialize(state);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UndoAsync(log.Id, "owner-1"));

        Assert.Null(await context.Flashcards.FindAsync(1));
        Assert.Empty(await context.ReviewSessions.ToListAsync());
        Assert.Null((await context.CardActionLogs.SingleAsync()).UndoneAt);
    }

    [Fact]
    public async Task UndoAsync_ReadsLegacyCardListSnapshot()
    {
        await using AppDbContext context = CreateContext();
        await SeedBaseAsync(context);
        CardActionService service = CreateService(context);
        CardActionLog log = await service.ExecuteAsync(
            new DeleteCardsCommand(context, 1, "owner-1", [1]));

        using JsonDocument state = JsonDocument.Parse(log.SnapshotJson);
        string legacySnapshot = state.RootElement.ValueKind == JsonValueKind.Object
            ? state.RootElement.GetProperty("Cards").GetRawText()
            : state.RootElement.GetRawText();
        log.SnapshotJson = legacySnapshot;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await service.UndoAsync(log.Id, "owner-1");

        Assert.NotNull(await context.Flashcards.FindAsync(1));
        Assert.NotNull((await context.CardActionLogs.SingleAsync()).UndoneAt);
    }

    [Fact]
    public async Task UndoAsync_RejectsMalformedSnapshotBeforeRestoringAnything()
    {
        await using AppDbContext context = CreateContext();
        await SeedBaseAsync(context);
        CardActionService service = CreateService(context);
        CardActionLog log = await service.ExecuteAsync(
            new DeleteCardsCommand(context, 1, "owner-1", [1]));
        log.SnapshotJson = "{\"Cards\":[null]}";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UndoAsync(log.Id, "owner-1"));

        Assert.Null(await context.Flashcards.FindAsync(1));
        Assert.Null((await context.CardActionLogs.SingleAsync()).UndoneAt);
    }

    private static async Task SeedBaseAsync(AppDbContext context)
    {
        FlashcardSet set = new()
        {
            Id = 1,
            UserId = "owner-1",
            Title = "Review set"
        };
        context.FlashcardSets.Add(set);
        context.Flashcards.AddRange(
            CreateCard(1, "hello", 0),
            CreateCard(2, "world", 1));
        context.StudySessions.Add(new StudySession
        {
            Id = 1,
            UserId = "owner-1",
            FlashcardSetId = 1,
            Mode = StudyMode.Dictation
        });
        context.UserProgresses.Add(new UserProgress
        {
            Id = 1,
            UserId = "owner-1",
            FlashcardId = 1,
            IsLearned = true,
            Status = UserProgressStatus.Mastered,
            CorrectCount = 7,
            WrongCount = 2,
            LastReviewed = DateTime.Parse("2026-01-01T10:00:00Z")
        });
        context.DictationSessionDetails.Add(new DictationSessionDetail
        {
            Id = 1,
            StudySessionId = 1,
            FlashcardId = 1,
            IsCorrect = true,
            AnsweredText = "hello",
            CreatedAt = DateTime.Parse("2026-01-01T10:01:00Z")
        });
        context.EnglishMissions.Add(new EnglishMission
        {
            Id = 1,
            StudySessionId = 1,
            Topic = "Travel",
            Title = "At the airport",
            Situation = "Check in",
            NpcName = "Alex",
            NpcRole = "Agent",
            OpeningLine = "May I see your passport?",
            GoalsJson = "[]",
            Status = "Active"
        });
        context.EnglishMissionTargetWords.Add(new EnglishMissionTargetWord
        {
            Id = 1,
            EnglishMissionId = 1,
            FlashcardId = 1,
            Term = "hello",
            Definition = "xin chào",
            PartOfSpeech = "interjection",
            ExampleSentence = "Hello there"
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static void AddReviewProgress(AppDbContext context, int id, int cardId)
    {
        context.ReviewProgresses.Add(new ReviewProgress
        {
            Id = id,
            UserId = "owner-1",
            FlashcardId = cardId,
            Stage = ReviewStage.Reviewing,
            NextReviewAtUtc = DateTimeOffset.Parse("2026-01-02T08:00:00+00:00"),
            LongTermIntervalDays = 23,
            LastRatedAtUtc = DateTimeOffset.Parse("2026-01-01T08:00:00+00:00")
        });
    }

    private static ReviewSession CreateSession(
        int id,
        string userId,
        DateTimeOffset startedAt,
        string? settings,
        params ReviewSessionItem[] items)
        => new()
        {
            Id = id,
            UserId = userId,
            FlashcardSetId = 1,
            SettingsSnapshotJson = settings,
            StartedAtUtc = startedAt,
            Items = items
        };

    private static ReviewSessionItem CreateReviewItem(
        int id,
        int sessionId,
        int cardId,
        int orderIndex)
        => new()
        {
            Id = id,
            ReviewSessionId = sessionId,
            FlashcardId = cardId,
            OrderIndex = orderIndex,
            IsNewCardAtAssignment = orderIndex == 0,
            NewCardAssignedDate = DateTime.Parse("2026-01-01"),
            Rating = orderIndex == 0 ? ReviewRating.Good : null,
            RatedAtUtc = orderIndex == 0
                ? DateTimeOffset.Parse("2026-01-01T08:30:00+00:00")
                : null,
            PreviousStage = ReviewStage.Learning,
            NextStage = ReviewStage.Reviewing,
            PreviousNextReviewAtUtc = DateTimeOffset.Parse("2026-01-01T08:00:00+00:00"),
            NextReviewAtUtc = DateTimeOffset.Parse("2026-01-02T08:00:00+00:00"),
            PreviousLongTermIntervalDays = 5,
            NextLongTermIntervalDays = 23
        };

    private static Flashcard CreateCard(int id, string text, int orderIndex)
        => new()
        {
            Id = id,
            FlashcardSetId = 1,
            FrontText = text,
            BackText = $"{text} meaning",
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = $"{text} sentence",
            ExampleMeaning = $"{text} example",
            Synonyms = $"{text}-synonym",
            ImageUrl = $"https://example.test/{text}.png",
            UploadedImagePath = $"/uploads/{text}.png",
            IsStarred = id == 1,
            OrderIndex = orderIndex
        };

    private static CardActionService CreateService(AppDbContext context)
    {
        CardActionCommandFactory factory = new(
        [
            new DeleteCardsCommandCreator(context),
            new StarCardsCommandCreator(context),
            new UnstarCardsCommandCreator(context)
        ]);
        return new CardActionService(context, factory);
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
}
