using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services;

public class QuizServiceTests
{
    [Fact]
    public async Task StartOrResume_creates_session_and_persisted_questions()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        var publisher = new RecordingStudyEventPublisher();
        QuizService service = CreateService(database.Context, publisher);

        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        Assert.Equal(StudyMode.Quiz, session.Mode);
        Assert.Equal(set.Id, session.FlashcardSetId);
        Assert.Equal(set.UserId, session.UserId);
        Assert.Null(session.Score);

        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .Where(question => question.StudySessionId == session.Id)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        Assert.Equal(4, questions.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, questions.Select(question => question.OrderIndex));
        Assert.All(questions, question => Assert.Equal(4, question.Choices.Count));
    }

    [Fact]
    public async Task StartOrResume_returns_existing_incomplete_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession first = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        StudySession resumed = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());

        Assert.Equal(first.Id, resumed.Id);
        Assert.Equal(1, await database.Context.StudySessions.CountAsync());
        Assert.Equal(4, await database.Context.QuizSessionQuestions.CountAsync());
    }

    [Fact]
    public async Task GetCurrentQuestion_returns_first_unanswered_with_session_counts()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion first = await database.Context.QuizSessionQuestions
            .OrderBy(question => question.OrderIndex)
            .FirstAsync();
        first.SelectedChoiceIndex = first.CorrectChoiceIndex;
        first.IsCorrect = true;
        first.AnsweredAt = DateTime.UtcNow;
        await database.Context.SaveChangesAsync();

        QuizQuestionState state = await service.GetCurrentQuestionAsync(
            set.Id,
            session.Id,
            set.UserId);

        Assert.Equal(session.Id, state.SessionId);
        Assert.Equal(set.Id, state.SetId);
        Assert.Equal(set.Title, state.SetTitle);
        Assert.Equal(4, state.TotalQuestions);
        Assert.Equal(1, state.AnsweredCount);
        Assert.Equal(1, state.CorrectCount);
        Assert.NotNull(state.Question);
        Assert.Equal(1, state.Question.OrderIndex);
        Assert.False(state.IsComplete);
    }

    [Fact]
    public async Task Answer_saves_correct_result_and_does_not_create_progress()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();

        QuizAnswerResult result = await service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            question.CorrectChoiceIndex,
            set.UserId);

        database.Context.ChangeTracker.Clear();
        QuizSessionQuestion stored = await database.Context.QuizSessionQuestions
            .SingleAsync(row => row.Id == question.Id);
        Assert.True(result.IsCorrect);
        Assert.Equal(question.CorrectChoiceIndex, result.CorrectChoiceIndex);
        Assert.False(result.IsLastQuestion);
        Assert.Equal(question.CorrectChoiceIndex, stored.SelectedChoiceIndex);
        Assert.True(stored.IsCorrect);
        Assert.NotNull(stored.AnsweredAt);
        Assert.Empty(await database.Context.UserProgresses.ToListAsync());
    }

    [Fact]
    public async Task Answer_same_choice_is_idempotent()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        int selectedChoice = question.CorrectChoiceIndex == 0 ? 1 : 0;

        QuizAnswerResult first = await service.AnswerAsync(
            set.Id, session.Id, question.Id, selectedChoice, set.UserId);
        QuizAnswerResult repeated = await service.AnswerAsync(
            set.Id, session.Id, question.Id, selectedChoice, set.UserId);

        Assert.Equal(first, repeated);
        Assert.False(repeated.IsCorrect);
        Assert.Equal(question.CorrectChoiceIndex, repeated.CorrectChoiceIndex);
    }

    [Fact]
    public async Task Answer_different_choice_throws_conflict()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        int firstChoice = question.CorrectChoiceIndex;
        int differentChoice = firstChoice == 0 ? 1 : 0;
        await service.AnswerAsync(
            set.Id, session.Id, question.Id, firstChoice, set.UserId);

        await Assert.ThrowsAsync<QuizConflictException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            differentChoice,
            set.UserId));
    }

    [Fact]
    public async Task Answer_rejects_question_from_another_session()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession firstSession = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();
        var anotherSession = new StudySession
        {
            FlashcardSetId = set.Id,
            UserId = set.UserId,
            Mode = StudyMode.Quiz
        };
        database.Context.StudySessions.Add(anotherSession);
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AnswerAsync(
            set.Id,
            anotherSession.Id,
            question.Id,
            question.CorrectChoiceIndex,
            set.UserId));

        database.Context.ChangeTracker.Clear();
        Assert.Null((await database.Context.QuizSessionQuestions
            .SingleAsync(row => row.Id == question.Id)).SelectedChoiceIndex);
        Assert.NotEqual(firstSession.Id, anotherSession.Id);
    }

    [Fact]
    public async Task Answer_rejects_another_user()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            question.CorrectChoiceIndex,
            "another-user"));

        database.Context.ChangeTracker.Clear();
        Assert.Null((await database.Context.QuizSessionQuestions
            .SingleAsync(row => row.Id == question.Id)).SelectedChoiceIndex);
    }

    [Fact]
    public async Task Last_answer_calculates_score_and_publishes_once()
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context, cardCount: 8);
        var publisher = new RecordingStudyEventPublisher();
        QuizService service = CreateService(database.Context, publisher);
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        List<QuizSessionQuestion> questions = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .OrderBy(row => row.OrderIndex)
            .ToListAsync();

        for (int index = 0; index < questions.Count - 1; index++)
        {
            QuizSessionQuestion question = questions[index];
            int selectedChoice = index == 0
                ? question.CorrectChoiceIndex
                : DifferentChoice(question.CorrectChoiceIndex);
            await service.AnswerAsync(
                set.Id, session.Id, question.Id, selectedChoice, set.UserId);
        }

        QuizSessionQuestion lastQuestion = questions[^1];
        int lastWrongChoice = DifferentChoice(lastQuestion.CorrectChoiceIndex);
        QuizAnswerResult result = await service.AnswerAsync(
            set.Id, session.Id, lastQuestion.Id, lastWrongChoice, set.UserId);
        QuizAnswerResult repeated = await service.AnswerAsync(
            set.Id, session.Id, lastQuestion.Id, lastWrongChoice, set.UserId);

        database.Context.ChangeTracker.Clear();
        StudySession storedSession = await database.Context.StudySessions
            .SingleAsync(row => row.Id == session.Id);
        Assert.True(result.IsLastQuestion);
        Assert.Equal(result, repeated);
        Assert.Equal(13, storedSession.Score);
        StudySessionCompletedEvent completion = Assert.Single(
            publisher.Events.OfType<StudySessionCompletedEvent>());
        Assert.Equal(StudyMode.Quiz, completion.Mode);
        Assert.Equal(13, completion.Score);
        Assert.Equal(set.Id, completion.SetId);
        Assert.Equal(session.Id, completion.SessionId);
        Assert.Equal(set.UserId, completion.UserId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task Answer_rejects_choice_index_outside_range(int selectedChoiceIndex)
    {
        await using var database = await QuizTestDatabase.CreateAsync();
        FlashcardSet set = await SeedQuestionPoolAsync(database.Context);
        QuizService service = CreateService(database.Context, new RecordingStudyEventPublisher());
        StudySession session = await service.StartOrResumeAsync(
            set.Id,
            set.UserId,
            new UserStudySettings());
        QuizSessionQuestion question = await database.Context.QuizSessionQuestions
            .AsNoTracking()
            .OrderBy(row => row.OrderIndex)
            .FirstAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.AnswerAsync(
            set.Id,
            session.Id,
            question.Id,
            selectedChoiceIndex,
            set.UserId));
    }

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

    private static QuizService CreateService(
        AppDbContext context,
        RecordingStudyEventPublisher publisher)
    {
        var questionFactory = new QuizQuestionFactory(context);
        var strategy = new QuizModeStrategy(
            new StudyCardQueryService(context),
            questionFactory);
        var resolver = new StudyModeStrategyResolver(new[] { strategy });
        return new QuizService(context, resolver, questionFactory, publisher);
    }

    private static int DifferentChoice(int correctChoiceIndex) =>
        correctChoiceIndex == 0 ? 1 : 0;

    private static async Task<FlashcardSet> SeedQuestionPoolAsync(
        AppDbContext context,
        int cardCount = 4)
    {
        var set = new FlashcardSet
        {
            Title = "Quiz lifecycle set",
            UserId = "quiz-user"
        };

        for (int index = 0; index < cardCount; index++)
        {
            set.Flashcards.Add(CreateCard(
                set,
                $"term {index}",
                $"definition {index}",
                index));
        }

        context.FlashcardSets.Add(set);
        await context.SaveChangesAsync();
        return set;
    }

    private sealed class RecordingStudyEventPublisher : IStudyEventPublisher
    {
        public List<StudyEvent> Events { get; } = new();

        public Task PublishAsync(
            StudyEvent studyEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(studyEvent);
            return Task.CompletedTask;
        }
    }

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
