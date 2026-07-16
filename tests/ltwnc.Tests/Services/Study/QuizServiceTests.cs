using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services;

public class QuizServiceTests
{
    [Fact]
    public async Task Schema_DuplicateOrderIndexWithinSession_ThrowsDbUpdateException()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        var (session, firstCard, secondCard) = await SeedQuizDataAsync(database.Context);

        database.Context.QuizSessionQuestions.AddRange(
            CreateQuestion(session.Id, firstCard.Id, orderIndex: 0),
            CreateQuestion(session.Id, secondCard.Id, orderIndex: 0));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Schema_DuplicateFlashcardWithinSession_ThrowsDbUpdateException()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        var (session, firstCard, _) = await SeedQuizDataAsync(database.Context);

        database.Context.QuizSessionQuestions.AddRange(
            CreateQuestion(session.Id, firstCard.Id, orderIndex: 0),
            CreateQuestion(session.Id, firstCard.Id, orderIndex: 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    private static async Task<(StudySession Session, Flashcard FirstCard, Flashcard SecondCard)>
        SeedQuizDataAsync(AppDbContext context)
    {
        var set = new FlashcardSet
        {
            Title = "Quiz schema set",
            UserId = "quiz-user"
        };
        var firstCard = CreateCard(set, "term one", "definition one", orderIndex: 0);
        var secondCard = CreateCard(set, "term two", "definition two", orderIndex: 1);
        var session = new StudySession
        {
            UserId = "quiz-user",
            FlashcardSet = set,
            Mode = StudyMode.Quiz
        };

        context.AddRange(set, firstCard, secondCard, session);
        await context.SaveChangesAsync();
        return (session, firstCard, secondCard);
    }

    private static Flashcard CreateCard(
        FlashcardSet set,
        string term,
        string definition,
        int orderIndex) => new()
    {
        FlashcardSet = set,
        FrontText = term,
        BackText = definition,
        Pronunciation = "/test/",
        PartOfSpeech = "noun",
        ExampleSentence = $"Example for {term}.",
        ExampleMeaning = $"Meaning for {term}.",
        OrderIndex = orderIndex
    };

    private static QuizSessionQuestion CreateQuestion(
        int sessionId,
        int flashcardId,
        int orderIndex) => new()
    {
        StudySessionId = sessionId,
        FlashcardId = flashcardId,
        OrderIndex = orderIndex,
        Direction = QuizQuestionDirection.TermToDefinition,
        PromptText = "prompt",
        Choice1Text = "correct",
        Choice2Text = "choice two",
        Choice3Text = "choice three",
        Choice4Text = "choice four",
        CorrectChoiceIndex = 0
    };

    private sealed class QuizTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private QuizTestDatabase(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<QuizTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            return new QuizTestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
