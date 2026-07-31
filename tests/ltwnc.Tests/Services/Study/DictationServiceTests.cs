using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using ltwnc.Services.StudyEvents;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services.Study;

public sealed class DictationServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetCardsForDictationAsync_ShuffleDisabled_ReturnsStrategyOrder()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        DictationService service = CreateService(context);

        List<Flashcard> actual = await service.GetCardsForDictationAsync(
            1, "user-1", new UserStudySettings { DictationShuffle = false });

        Assert.Equal(new[] { cards[0].Id, cards[1].Id, cards[2].Id }, actual.Select(card => card.Id));
    }

    [Fact]
    public async Task GetCardsForDictationAsync_ShuffleEnabled_ReturnsSameCardsWithoutMutatingSource()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        var strategy = new StubStrategy(cards);
        var resolver = new Mock<IStudyModeStrategyResolver>();
        resolver.Setup(value => value.Resolve(StudyMode.Dictation)).Returns(strategy);
        var service = new DictationService(context, resolver.Object, new RecordingPublisher());

        List<Flashcard> actual = await service.GetCardsForDictationAsync(
            1, "user-1", new UserStudySettings { DictationShuffle = true });

        Assert.Equal(new[] { 1, 2, 3 }, actual.Select(card => card.Id).OrderBy(id => id));
        Assert.Equal(new[] { 1, 2, 3 }, cards.Select(card => card.Id));
        Assert.NotSame(cards, actual);
    }

    [Fact]
    public async Task AnyCardHasExampleSentenceAsync_SetContainsNonBlankSentence_ReturnsTrue()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAndCardsAsync(context);

        bool actual = await CreateService(context).AnyCardHasExampleSentenceAsync(1);

        Assert.True(actual);
    }

    [Fact]
    public async Task AnyCardHasExampleSentenceAsync_SetContainsOnlyBlankSentences_ReturnsFalse()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        cards.ForEach(card => card.ExampleSentence = "   ");
        await context.SaveChangesAsync();

        bool actual = await CreateService(context).AnyCardHasExampleSentenceAsync(1);

        Assert.False(actual);
    }

    [Fact]
    public async Task CreateSessionAsync_CardsProvided_CreatesSessionAndQuestionSnapshots()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);

        StudySession actual = await CreateService(context).CreateSessionAsync(
            "user-1", 1, DictationContentMode.Vocabulary, cards.Count, cards);

        Assert.Equal(StudyMode.Dictation, actual.Mode);
        Assert.Equal(3, actual.PlannedItemCount);
        Assert.Equal(FixedNow.UtcDateTime, actual.StartedAt);
        List<DictationSessionQuestion> questions = await context.DictationSessionQuestions
            .OrderBy(question => question.OrderIndex)
            .ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, questions.Select(question => question.FlashcardId));
        Assert.Equal("alpha", questions[0].PromptText);
        Assert.Equal("alpha", questions[0].CorrectAnswer);
    }

    [Fact]
    public async Task CreateSessionAsync_ExampleSentenceMode_SnapshotsExampleSentence()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);

        StudySession session = await CreateService(context).CreateSessionAsync(
            "user-1", 1, DictationContentMode.ExampleSentence, cards.Count, cards);

        DictationSessionQuestion question = await context.DictationSessionQuestions
            .SingleAsync(row => row.StudySessionId == session.Id && row.FlashcardId == 1);
        Assert.Equal("Alpha comes first!", question.PromptText);
        Assert.Equal("Alpha comes first!", question.CorrectAnswer);
        Assert.Equal(DictationContentMode.ExampleSentence, session.DictationContentMode);
    }

    [Fact]
    public async Task CreateSessionAsync_NoCardsAndNegativePlannedCount_StoresZeroPlannedItems()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAndCardsAsync(context);

        StudySession actual = await CreateService(context).CreateSessionAsync(
            "user-1", 1, plannedItemCount: -5, cards: null);

        Assert.Equal(0, actual.PlannedItemCount);
        Assert.Empty(await context.DictationSessionQuestions.ToListAsync());
    }

    [Fact]
    public async Task CheckAnswerAsync_ExactVocabularyAnswer_PersistsCorrectResult()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "alpha", "user-1", acceptSynonyms: false);

        Assert.True(actual.IsCorrect);
        Assert.Equal("alpha", actual.CorrectAnswer);
        Assert.Null(actual.Hint);
        DictationSessionDetail detail = await context.DictationSessionDetails.SingleAsync();
        Assert.True(detail.IsCorrect);
        Assert.Equal("alpha", detail.AnsweredText);
    }

    [Fact]
    public async Task CheckAnswerAsync_DifferentCaseWhitespaceAndPunctuation_ReturnsCorrect()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(
            context, DictationContentMode.ExampleSentence);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "  ALPHA   comes first  ", "user-1", false);

        Assert.True(actual.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_WrongVocabularyAnswer_ReturnsAnswerAndHint()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "wrong", "user-1", false);

        Assert.False(actual.IsCorrect);
        Assert.Equal("alpha", actual.CorrectAnswer);
        Assert.Equal("IPA: /a/ | Nghĩa: first", actual.Hint);
        Assert.Empty(actual.WordComparison);
    }

    [Fact]
    public async Task CheckAnswerAsync_VocabularySynonymEnabled_AcceptsDelimitedSynonym()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "beginning", "user-1", acceptSynonyms: true);

        Assert.True(actual.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_VocabularySynonymDisabled_RejectsSynonym()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "beginning", "user-1", acceptSynonyms: false);

        Assert.False(actual.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_ExampleSentenceMode_IgnoresVocabularySynonyms()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(
            context, DictationContentMode.ExampleSentence);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "beginning", "user-1", acceptSynonyms: true);

        Assert.False(actual.IsCorrect);
        Assert.Equal("First letter", actual.ExampleMeaning);
    }

    [Fact]
    public async Task CheckAnswerAsync_ExampleSentenceWithSubstitution_ReturnsIncorrectComparison()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(
            context, DictationContentMode.ExampleSentence);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "Alpha goes first", "user-1", false);

        Assert.Equal(
            new[] { DictationWordStatus.Correct, DictationWordStatus.Incorrect, DictationWordStatus.Correct },
            actual.WordComparison.Select(word => word.Status));
        Assert.Equal("goes", actual.WordComparison[1].AnsweredWord);
        Assert.Equal("comes", actual.WordComparison[1].CorrectWord);
    }

    [Fact]
    public async Task CheckAnswerAsync_ExampleSentenceWithMissingWord_ReturnsMissingComparison()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(
            context, DictationContentMode.ExampleSentence);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "Alpha first", "user-1", false);

        DictationWordComparison missing = Assert.Single(
            actual.WordComparison, word => word.Status == DictationWordStatus.Missing);
        Assert.Null(missing.AnsweredWord);
        Assert.Equal("comes", missing.CorrectWord);
    }

    [Fact]
    public async Task CheckAnswerAsync_ExampleSentenceWithExtraWord_ReturnsExtraComparison()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(
            context, DictationContentMode.ExampleSentence);

        DictationCheckResult actual = await service.CheckAnswerAsync(
            session.Id, 1, 1, "Alpha always comes first", "user-1", false);

        DictationWordComparison extra = Assert.Single(
            actual.WordComparison, word => word.Status == DictationWordStatus.Extra);
        Assert.Equal("always", extra.AnsweredWord);
        Assert.Null(extra.CorrectWord);
    }

    [Fact]
    public async Task CheckAnswerAsync_CardOutsideSessionSnapshot_ThrowsKeyNotFoundException()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context, cardCount: 1);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CheckAnswerAsync(
            session.Id, 1, 2, "beta", "user-1", false));

        Assert.Empty(await context.DictationSessionDetails.ToListAsync());
        Assert.Empty(await context.UserProgresses.ToListAsync());
    }

    [Fact]
    public async Task CheckAnswerAsync_CompletedSession_ThrowsInvalidOperationException()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context, cardCount: 1);
        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);
        await service.CompleteSessionAsync(session.Id, 1, "user-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckAnswerAsync(
            session.Id, 1, 1, "alpha", "user-1", false));
    }

    [Fact]
    public async Task CheckAnswerAsync_DuplicateSubmission_ReturnsOriginalResultWithoutDuplicateSideEffects()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context, cardCount: 1);
        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);

        DictationCheckResult duplicate = await service.CheckAnswerAsync(
            session.Id, 1, 1, "wrong", "user-1", false);

        Assert.True(duplicate.IsCorrect);
        Assert.Single(await context.DictationSessionDetails.ToListAsync());
        UserProgress progress = await context.UserProgresses.SingleAsync();
        Assert.Equal(1, progress.CorrectCount);
        Assert.Equal(0, progress.WrongCount);
    }

    [Fact]
    public async Task CheckAnswerAsync_CorrectAnswer_UpdatesGeneralLearningProgress()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context, cardCount: 1);

        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);

        UserProgress actual = await context.UserProgresses.SingleAsync();
        Assert.True(actual.IsLearned);
        Assert.Equal(UserProgressStatus.Mastered, actual.Status);
        Assert.Equal(1, actual.CorrectCount);
        Assert.Equal(FixedNow.UtcDateTime, actual.LastReviewed);
    }

    [Fact]
    public async Task CheckAnswerAsync_WrongAnswer_UpdatesGeneralLearningProgress()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context, cardCount: 1);
        context.UserProgresses.Add(new UserProgress
        {
            UserId = "user-1", FlashcardId = 1, IsLearned = true,
            Status = UserProgressStatus.Mastered, CorrectCount = 2
        });
        await context.SaveChangesAsync();

        await service.CheckAnswerAsync(session.Id, 1, 1, "wrong", "user-1", false);

        UserProgress actual = await context.UserProgresses.SingleAsync();
        Assert.False(actual.IsLearned);
        Assert.Equal(UserProgressStatus.Learning, actual.Status);
        Assert.Equal(2, actual.CorrectCount);
        Assert.Equal(1, actual.WrongCount);
    }

    [Fact]
    public async Task CheckAnswerAsync_SavedSuccessfully_PublishesAnswerCheckedEvent()
    {
        await using AppDbContext context = CreateContext();
        var publisher = new RecordingPublisher();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(
            context, publisher: publisher, cardCount: 1);

        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);

        var actual = Assert.IsType<DictationAnswerCheckedEvent>(Assert.Single(publisher.Events));
        Assert.Equal("user-1", actual.UserId);
        Assert.Equal(session.Id, actual.SessionId);
        Assert.Equal(1, actual.FlashcardId);
        Assert.True(actual.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_SessionOwnedByAnotherUser_ThrowsUnauthorizedAccessException()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context, cardCount: 1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CheckAnswerAsync(
            session.Id, 1, 1, "alpha", "user-2", false));
    }

    [Fact]
    public async Task CompleteSessionAsync_UnansweredQuestion_ThrowsInvalidOperationException()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context, cardCount: 2);
        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSessionAsync(session.Id, 1, "user-1"));

        Assert.Null(session.CompletedAt);
    }

    [Fact]
    public async Task CompleteSessionAsync_AllQuestionsAnswered_CalculatesRoundedScore()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context);
        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);
        await service.CheckAnswerAsync(session.Id, 1, 2, "beta", "user-1", false);
        await service.CheckAnswerAsync(session.Id, 1, 3, "wrong", "user-1", false);

        StudySession actual = await service.CompleteSessionAsync(session.Id, 1, "user-1");

        Assert.Equal(67, actual.Score);
        Assert.Equal(FixedNow.UtcDateTime, actual.CompletedAt);
        Assert.Equal(0, actual.DurationSeconds);
    }

    [Fact]
    public async Task CompleteSessionAsync_AlreadyCompleted_DoesNotRepublishEvent()
    {
        await using AppDbContext context = CreateContext();
        var publisher = new RecordingPublisher();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(
            context, publisher: publisher, cardCount: 1);
        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);
        await service.CompleteSessionAsync(session.Id, 1, "user-1");
        int eventCount = publisher.Events.Count;

        StudySession actual = await service.CompleteSessionAsync(session.Id, 1, "user-1");

        Assert.Same(session, actual);
        Assert.Equal(eventCount, publisher.Events.Count);
    }

    [Fact]
    public async Task CompleteSessionAsync_SavedSuccessfully_PublishesCompletedEvent()
    {
        await using AppDbContext context = CreateContext();
        var publisher = new RecordingPublisher();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(
            context, publisher: publisher, cardCount: 1);
        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);

        await service.CompleteSessionAsync(session.Id, 1, "user-1");

        var actual = Assert.IsType<StudySessionCompletedEvent>(publisher.Events.Last());
        Assert.Equal(StudyMode.Dictation, actual.Mode);
        Assert.Equal(100, actual.Score);
        Assert.Equal(session.Id, actual.SessionId);
    }

    [Fact]
    public async Task GetRetryPlanAsync_CompletedSession_ReturnsOnlyWrongCardsInOriginalOrder()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context);
        await service.CheckAnswerAsync(session.Id, 1, 1, "wrong", "user-1", false);
        await service.CheckAnswerAsync(session.Id, 1, 2, "beta", "user-1", false);
        await service.CheckAnswerAsync(session.Id, 1, 3, "wrong", "user-1", false);
        await service.CompleteSessionAsync(session.Id, 1, "user-1");

        DictationRetryPlan actual = await service.GetRetryPlanAsync(session.Id, 1, "user-1");

        Assert.Equal(new[] { 1, 3 }, actual.Cards.Select(card => card.Id));
        Assert.Equal(DictationContentMode.Vocabulary, actual.ContentMode);
    }

    [Fact]
    public async Task GetRetryPlanAsync_IncompleteSession_ThrowsInvalidOperationException()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetRetryPlanAsync(session.Id, 1, "user-1"));
    }

    [Fact]
    public async Task GetHistoryAsync_WrongAnsweredQuestions_ReturnsNewestFirst()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context, cardCount: 2);
        await service.CheckAnswerAsync(session.Id, 1, 1, "wrong one", "user-1", false);
        await service.CheckAnswerAsync(session.Id, 1, 2, "wrong two", "user-1", false);
        DictationSessionQuestion olderQuestion = await context.DictationSessionQuestions
            .SingleAsync(question => question.FlashcardId == 1);
        olderQuestion.AnsweredAt = FixedNow.AddMinutes(-1).UtcDateTime;
        await context.SaveChangesAsync();

        List<DictationHistoryItem> actual = await service.GetHistoryAsync(1, "user-1");

        Assert.Equal(2, actual.Count);
        Assert.Equal(new[] { "wrong two", "wrong one" }, actual.Select(item => item.AnsweredText));
        Assert.All(actual, item => Assert.NotEqual(item.AnsweredText, item.CorrectAnswer));
    }

    [Fact]
    public async Task GetHistoryAsync_UserDoesNotOwnSet_ThrowsUnauthorizedAccessException()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetAndCardsAsync(context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(context).GetHistoryAsync(1, "user-2"));
    }

    [Fact]
    public async Task GetSessionResultAsync_CompletedSession_ReturnsSnapshotCountsAndWrongCards()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, List<Flashcard> cards) =
            await CreateSessionAsync(context, cardCount: 2);
        await service.CheckAnswerAsync(session.Id, 1, 1, "alpha", "user-1", false);
        await service.CheckAnswerAsync(session.Id, 1, 2, "wrong", "user-1", false);
        await service.CompleteSessionAsync(session.Id, 1, "user-1");
        cards[1].FrontText = "changed later";
        await context.SaveChangesAsync();

        DictationResult actual = await service.GetSessionResultAsync(session.Id, 1, "user-1");

        Assert.Equal(2, actual.TotalCards);
        Assert.Equal(1, actual.CorrectCount);
        Assert.Equal(50, actual.Score);
        DictationResultCard wrong = Assert.Single(actual.WrongCards);
        Assert.Equal("beta", wrong.Term);
    }

    [Fact]
    public async Task GetSessionResultAsync_IncompleteSession_ThrowsInvalidOperationException()
    {
        await using AppDbContext context = CreateContext();
        (DictationService service, StudySession session, _) = await CreateSessionAsync(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetSessionResultAsync(session.Id, 1, "user-1"));
    }

    private static async Task<(DictationService Service, StudySession Session, List<Flashcard> Cards)>
        CreateSessionAsync(
            AppDbContext context,
            DictationContentMode mode = DictationContentMode.Vocabulary,
            RecordingPublisher? publisher = null,
            int cardCount = 3)
    {
        List<Flashcard> cards = await SeedSetAndCardsAsync(context);
        DictationService service = CreateService(context, publisher);
        StudySession session = await service.CreateSessionAsync(
            "user-1", 1, mode, cardCount, cards.Take(cardCount).ToList());
        return (service, session, cards);
    }

    private static DictationService CreateService(
        AppDbContext context,
        RecordingPublisher? publisher = null)
    {
        var queryService = new StudyCardQueryService(context);
        var resolver = new StudyModeStrategyResolver(new IStudyModeStrategy[]
        {
            new DictationModeStrategy(queryService)
        });
        return new DictationService(
            context,
            resolver,
            publisher ?? new RecordingPublisher(),
            new FixedTimeProvider(FixedNow));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<List<Flashcard>> SeedSetAndCardsAsync(AppDbContext context)
    {
        if (await context.FlashcardSets.AnyAsync())
        {
            return await context.Flashcards.OrderBy(card => card.OrderIndex).ToListAsync();
        }

        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            UserId = "user-1",
            Title = "Dictation set"
        });
        var cards = new List<Flashcard>
        {
            CreateCard(1, "alpha", "first", "/a/", "Alpha comes first!", "First letter", "beginning; start", 0),
            CreateCard(2, "beta", "second", "/b/", "Beta comes second.", "Second letter", null, 1),
            CreateCard(3, "gamma", "third", "/g/", "Gamma comes third.", "Third letter", null, 2)
        };
        context.Flashcards.AddRange(cards);
        await context.SaveChangesAsync();
        return cards;
    }

    private static Flashcard CreateCard(
        int id,
        string term,
        string definition,
        string pronunciation,
        string sentence,
        string meaning,
        string? synonyms,
        int orderIndex) => new()
        {
            Id = id,
            FlashcardSetId = 1,
            FrontText = term,
            BackText = definition,
            Pronunciation = pronunciation,
            PartOfSpeech = "noun",
            ExampleSentence = sentence,
            ExampleMeaning = meaning,
            Synonyms = synonyms,
            OrderIndex = orderIndex
        };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingPublisher : IStudyEventPublisher
    {
        public List<StudyEvent> Events { get; } = new();

        public Task PublishAsync(StudyEvent studyEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(studyEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class StubStrategy(List<Flashcard> cards) : IStudyModeStrategy
    {
        public StudyMode Mode => StudyMode.Dictation;

        public Task<List<Flashcard>> GetCardsAsync(
            int setId,
            UserStudySettings settings,
            string? userId) => Task.FromResult(cards);

        public ltwnc.Models.ViewModels.Study.StudyModeOptionViewModel BuildOption(
            int setId,
            IReadOnlyList<Flashcard> source,
            UserStudySettings settings) => throw new NotSupportedException();
    }
}
