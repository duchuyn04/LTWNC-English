using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.CardActions;
using ltwnc.Services.FlashcardSets;
using ltwnc.Services.Study;
using ltwnc.Services.StudyModes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ltwnc.Tests.Integration;

public class QuizCardDeletionTests
{
    [Fact]
    public async Task DeleteCardAsync_keeps_completed_quiz_snapshot_accessible()
    {
        await using QuizDeletionDatabase database = await QuizDeletionDatabase.CreateAsync();
        (Flashcard card, StudySession session) = await SeedCompletedQuizAsync(database.Context);
        var service = new FlashcardSetService(database.Context, null!);

        await service.DeleteCardAsync(card.Id, "owner");

        Assert.Null(await database.Context.Flashcards.FindAsync(card.Id));
        QuizSessionResult result = await CreateQuizService(database.Context)
            .GetResultAsync(session.FlashcardSetId, session.Id, "owner");
        QuizWrongAnswer wrong = Assert.Single(result.WrongAnswers);
        Assert.Equal("Stored prompt", wrong.PromptText);
        Assert.Equal("Stored wrong", wrong.SelectedAnswer);
        Assert.Equal("Stored correct", wrong.CorrectAnswer);
    }

    [Fact]
    public async Task DeleteCardsCommand_keeps_completed_quiz_snapshot_accessible()
    {
        await using QuizDeletionDatabase database = await QuizDeletionDatabase.CreateAsync();
        (Flashcard card, StudySession session) = await SeedCompletedQuizAsync(database.Context);
        var command = new DeleteCardsCommand(
            database.Context,
            session.FlashcardSetId,
            "owner",
            new[] { card.Id });

        await command.ExecuteAsync();

        Assert.Null(await database.Context.Flashcards.FindAsync(card.Id));
        QuizSessionResult result = await CreateQuizService(database.Context)
            .GetResultAsync(session.FlashcardSetId, session.Id, "owner");
        Assert.Equal("Stored prompt", Assert.Single(result.WrongAnswers).PromptText);
    }

    [Fact]
    public async Task DeleteCardAsync_failure_does_not_partially_delete_progress()
    {
        await using QuizDeletionDatabase database = await QuizDeletionDatabase.CreateAsync();
        (Flashcard card, StudySession session) = await SeedCompletedQuizAsync(database.Context);
        database.Context.UserProgresses.Add(new UserProgress
        {
            UserId = "owner",
            FlashcardId = card.Id,
            Status = UserProgressStatus.Learning
        });
        database.Context.DictationSessionDetails.Add(new DictationSessionDetail
        {
            StudySessionId = session.Id,
            FlashcardId = card.Id,
            AnsweredText = "answer"
        });
        await database.Context.SaveChangesAsync();
        var service = new FlashcardSetService(database.Context, null!);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.DeleteCardAsync(card.Id, "owner"));

        database.Context.ChangeTracker.Clear();
        Assert.True(await database.Context.UserProgresses.AnyAsync(row =>
            row.FlashcardId == card.Id));
        Assert.NotNull(await database.Context.Flashcards.FindAsync(card.Id));
    }

    private static QuizService CreateQuizService(AppDbContext context) => new(
        context,
        Mock.Of<IStudyModeStrategyResolver>(),
        new QuizQuestionFactory(context),
        TestStudyEvents.NoOpPublisher());

    private static async Task<(Flashcard Card, StudySession Session)> SeedCompletedQuizAsync(
        AppDbContext context)
    {
        var set = new FlashcardSet { Title = "Set", UserId = "owner" };
        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();
        var card = new Flashcard
        {
            FlashcardSetId = set.Id,
            FrontText = "front",
            BackText = "back"
        };
        context.Flashcards.Add(card);
        await context.SaveChangesAsync();
        var session = new StudySession
        {
            FlashcardSetId = set.Id,
            UserId = "owner",
            Mode = StudyMode.Quiz,
            Score = 0,
            CompletedAt = DateTime.UtcNow
        };
        context.StudySessions.Add(session);
        context.QuizSessionQuestions.Add(new QuizSessionQuestion
        {
            StudySession = session,
            FlashcardId = card.Id,
            OrderIndex = 0,
            Direction = QuizQuestionDirection.TermToDefinition,
            PromptText = "Stored prompt",
            Choice1Text = "Stored correct",
            Choice2Text = "Stored wrong",
            Choice3Text = "Stored third",
            Choice4Text = "Stored fourth",
            CorrectChoiceIndex = 0,
            SelectedChoiceIndex = 1,
            IsCorrect = false,
            AnsweredAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return (card, session);
    }

    private sealed class QuizDeletionDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Context { get; }

        private QuizDeletionDatabase(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public static async Task<QuizDeletionDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new QuizDeletionDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
