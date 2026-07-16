using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services;

public class QuizQuestionFactoryTests
{
    [Fact]
    public async Task BuildQuestions_shuffles_cards_and_assigns_sequential_order_indexes()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(context, sameSetCardCount: 4);
        var directions = cards.ToDictionary(
            card => card.Id,
            _ => QuizQuestionDirection.TermToDefinition);

        var factory = new QuizQuestionFactory(context, new Random(17));
        List<QuizSessionQuestion> questions = await factory.BuildQuestionsAsync(
            1,
            "user-1",
            cards,
            directions);

        Assert.Equal(cards.Count, questions.Count);
        Assert.Equal(
            cards.Select(card => card.Id).OrderBy(id => id),
            questions.Select(question => question.FlashcardId).OrderBy(id => id));
        Assert.NotEqual(
            cards.Select(card => card.Id),
            questions.Select(question => question.FlashcardId));
        Assert.Equal(Enumerable.Range(0, cards.Count), questions.Select(question => question.OrderIndex));
        Assert.All(questions, question =>
        {
            Flashcard card = cards.Single(candidate => candidate.Id == question.FlashcardId);
            Assert.Equal(card.FrontText, question.PromptText);
        });
    }

    [Fact]
    public async Task BuildQuestions_balances_directions_with_difference_at_most_one()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(context, sameSetCardCount: 5);

        var factory = new QuizQuestionFactory(context);
        List<QuizSessionQuestion> questions = await factory.BuildQuestionsAsync(
            1,
            "user-1",
            cards);

        int termToDefinition = questions.Count(question =>
            question.Direction == QuizQuestionDirection.TermToDefinition);
        int definitionToTerm = questions.Count(question =>
            question.Direction == QuizQuestionDirection.DefinitionToTerm);

        Assert.True(Math.Abs(termToDefinition - definitionToTerm) <= 1);
    }

    [Fact]
    public async Task BuildQuestions_creates_four_distinct_choices_with_one_correct_answer()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(context, sameSetCardCount: 4);
        context.Flashcards.Add(CreateCard(50, 2, " TERM 1 ", " definition 1 "));
        await context.SaveChangesAsync();

        var directions = cards.ToDictionary(
            card => card.Id,
            _ => QuizQuestionDirection.TermToDefinition);
        var factory = new QuizQuestionFactory(context);

        List<QuizSessionQuestion> questions = await factory.BuildQuestionsAsync(
            1,
            "user-1",
            cards,
            directions);

        Assert.All(questions, question =>
        {
            Assert.Equal(4, question.Choices.Count);
            Assert.Equal(4, question.Choices.Select(Normalize).Distinct().Count());
            Assert.InRange(question.CorrectChoiceIndex, 0, 3);

            Flashcard card = cards.Single(candidate => candidate.Id == question.FlashcardId);
            string correctAnswer = question.Direction == QuizQuestionDirection.TermToDefinition
                ? card.BackText
                : card.FrontText;
            Assert.Equal(
                1,
                question.Choices.Count(choice => Normalize(choice) == Normalize(correctAnswer)));
            Assert.Equal(
                Normalize(correctAnswer),
                Normalize(question.Choices[question.CorrectChoiceIndex]));
        });
    }

    [Fact]
    public async Task BuildQuestions_prefers_same_set_distractors()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(context, sameSetCardCount: 4);
        var factory = new QuizQuestionFactory(context);

        QuizSessionQuestion question = Assert.Single(await factory.BuildQuestionsAsync(
            1,
            "user-1",
            new[] { cards[0] },
            new Dictionary<int, QuizQuestionDirection>
            {
                [cards[0].Id] = QuizQuestionDirection.TermToDefinition
            }));

        HashSet<string> expectedSameSetChoices = cards
            .Select(card => Normalize(card.BackText))
            .ToHashSet();
        Assert.Equal(expectedSameSetChoices, question.Choices.Select(Normalize).ToHashSet());
        Assert.DoesNotContain(question.Choices, choice => Normalize(choice) == Normalize("owned definition 1"));
    }

    [Fact]
    public async Task BuildQuestions_falls_back_to_owned_sets_only()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(context, sameSetCardCount: 2);
        var factory = new QuizQuestionFactory(context);

        QuizSessionQuestion question = Assert.Single(await factory.BuildQuestionsAsync(
            1,
            "user-1",
            new[] { cards[0] },
            new Dictionary<int, QuizQuestionDirection>
            {
                [cards[0].Id] = QuizQuestionDirection.TermToDefinition
            }));

        HashSet<string> allowed = context.Flashcards
            .AsEnumerable()
            .Where(card => card.FlashcardSetId == 1 || card.FlashcardSetId == 2)
            .Select(card => Normalize(card.BackText))
            .ToHashSet();

        Assert.All(question.Choices, choice => Assert.Contains(Normalize(choice), allowed));
        Assert.Contains(question.Choices, choice => Normalize(choice) == Normalize(cards[1].BackText));
        Assert.Contains(question.Choices, choice => choice.StartsWith("owned definition", StringComparison.Ordinal));
        Assert.DoesNotContain(question.Choices, choice => choice.StartsWith("foreign definition", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildQuestions_honors_fixed_directions()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(context, sameSetCardCount: 4);
        var fixedDirections = new Dictionary<int, QuizQuestionDirection>
        {
            [cards[0].Id] = QuizQuestionDirection.DefinitionToTerm,
            [cards[1].Id] = QuizQuestionDirection.TermToDefinition
        };
        var factory = new QuizQuestionFactory(context);

        List<QuizSessionQuestion> questions = await factory.BuildQuestionsAsync(
            1,
            "user-1",
            cards.Take(2).ToList(),
            fixedDirections);

        QuizSessionQuestion firstCardQuestion = questions.Single(question =>
            question.FlashcardId == cards[0].Id);
        QuizSessionQuestion secondCardQuestion = questions.Single(question =>
            question.FlashcardId == cards[1].Id);
        Assert.Equal(QuizQuestionDirection.DefinitionToTerm, firstCardQuestion.Direction);
        Assert.Equal(cards[0].BackText, firstCardQuestion.PromptText);
        Assert.Equal(QuizQuestionDirection.TermToDefinition, secondCardQuestion.Direction);
        Assert.Equal(cards[1].FrontText, secondCardQuestion.PromptText);
    }

    [Fact]
    public async Task BuildQuestions_balances_unfixed_directions_around_partial_fixed_directions()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(context, sameSetCardCount: 4);
        var fixedDirections = new Dictionary<int, QuizQuestionDirection>
        {
            [cards[0].Id] = QuizQuestionDirection.TermToDefinition
        };
        var factory = new QuizQuestionFactory(context);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            List<QuizSessionQuestion> questions = await factory.BuildQuestionsAsync(
                1,
                "user-1",
                cards,
                fixedDirections);

            Assert.Equal(
                QuizQuestionDirection.TermToDefinition,
                questions.Single(question => question.FlashcardId == cards[0].Id).Direction);
            int termToDefinition = questions.Count(question =>
                question.Direction == QuizQuestionDirection.TermToDefinition);
            int definitionToTerm = questions.Count(question =>
                question.Direction == QuizQuestionDirection.DefinitionToTerm);
            Assert.True(Math.Abs(termToDefinition - definitionToTerm) <= 1);
        }
    }

    [Fact]
    public async Task BuildQuestions_deduplicates_normalized_values_across_same_and_owned_pools()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetsAsync(context);
        Flashcard source = CreateCard(1, 1, "source", "correct");
        context.Flashcards.AddRange(
            source,
            CreateCard(2, 1, "same-1", "Alpha"),
            CreateCard(3, 1, "same-2", "Beta"),
            CreateCard(101, 2, "owned-1", " alpha "),
            CreateCard(102, 2, "owned-2", "Gamma"),
            CreateCard(103, 2, "owned-3", "Delta"),
            CreateCard(201, 3, "foreign", "Foreign"));
        await context.SaveChangesAsync();
        var factory = new QuizQuestionFactory(context, new Random(7));

        QuizSessionQuestion question = Assert.Single(await factory.BuildQuestionsAsync(
            1,
            "user-1",
            new[] { source },
            new Dictionary<int, QuizQuestionDirection>
            {
                [source.Id] = QuizQuestionDirection.TermToDefinition
            }));

        Assert.Equal(4, question.Choices.Select(Normalize).Distinct().Count());
        Assert.DoesNotContain(question.Choices, choice => Normalize(choice) == "FOREIGN");
        Assert.Contains(question.Choices, choice => Normalize(choice) == "GAMMA"
            || Normalize(choice) == "DELTA");
    }

    [Fact]
    public async Task BuildQuestions_large_pool_uses_bounded_random_operations_per_question()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(
            context,
            sameSetCardCount: 1_000,
            ownedOtherCardCount: 0,
            foreignCardCount: 0);
        var random = new CountingRandom();
        var factory = new QuizQuestionFactory(context, random);

        QuizSessionQuestion question = Assert.Single(await factory.BuildQuestionsAsync(
            1,
            "user-1",
            new[] { cards[0] },
            new Dictionary<int, QuizQuestionDirection>
            {
                [cards[0].Id] = QuizQuestionDirection.TermToDefinition
            }));

        Assert.Equal(4, question.Choices.Select(Normalize).Distinct().Count());
        Assert.InRange(random.NextCalls, 1, 32);
    }

    [Fact]
    public async Task BuildQuestions_throws_when_only_another_users_cards_can_fill_choices()
    {
        await using AppDbContext context = CreateContext();
        List<Flashcard> cards = await SeedLibraryAsync(
            context,
            sameSetCardCount: 2,
            ownedOtherCardCount: 0,
            foreignCardCount: 4);
        var factory = new QuizQuestionFactory(context);

        await Assert.ThrowsAsync<QuizUnavailableException>(() => factory.BuildQuestionsAsync(
            1,
            "user-1",
            new[] { cards[0] },
            new Dictionary<int, QuizQuestionDirection>
            {
                [cards[0].Id] = QuizQuestionDirection.TermToDefinition
            }));
    }

    [Fact]
    public async Task GetAvailability_requires_four_distinct_terms_and_definitions()
    {
        await using AppDbContext context = CreateContext();
        await SeedSetsAsync(context);
        context.Flashcards.AddRange(
            CreateCard(1, 1, "Alpha", "One"),
            CreateCard(2, 1, " bravo ", "Two"),
            CreateCard(3, 1, "CHARLIE", "Three"),
            CreateCard(4, 2, "delta", " three "),
            CreateCard(5, 3, "Foreign term", "Foreign definition"));
        await context.SaveChangesAsync();
        var factory = new QuizQuestionFactory(context);

        QuizPoolAvailability unavailable = await factory.GetAvailabilityAsync(1, "user-1");

        Assert.False(unavailable.IsAvailable);
        Assert.Equal(4, unavailable.DistinctTermCount);
        Assert.Equal(3, unavailable.DistinctDefinitionCount);
        Assert.NotNull(unavailable.UnavailableReason);

        context.Flashcards.Add(CreateCard(6, 2, " ALPHA ", "Four"));
        await context.SaveChangesAsync();

        QuizPoolAvailability available = await factory.GetAvailabilityAsync(1, "user-1");

        Assert.True(available.IsAvailable);
        Assert.Equal(4, available.DistinctTermCount);
        Assert.Equal(4, available.DistinctDefinitionCount);
        Assert.Null(available.UnavailableReason);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<List<Flashcard>> SeedLibraryAsync(
        AppDbContext context,
        int sameSetCardCount,
        int ownedOtherCardCount = 3,
        int foreignCardCount = 3)
    {
        await SeedSetsAsync(context);

        var sameSetCards = Enumerable.Range(1, sameSetCardCount)
            .Select(index => CreateCard(index, 1, $"term {index}", $"definition {index}"))
            .ToList();
        var ownedCards = Enumerable.Range(1, ownedOtherCardCount)
            .Select(index => CreateCard(100 + index, 2, $"owned term {index}", $"owned definition {index}"));
        var foreignCards = Enumerable.Range(1, foreignCardCount)
            .Select(index => CreateCard(200 + index, 3, $"foreign term {index}", $"foreign definition {index}"));

        context.Flashcards.AddRange(sameSetCards);
        context.Flashcards.AddRange(ownedCards);
        context.Flashcards.AddRange(foreignCards);
        await context.SaveChangesAsync();
        return sameSetCards;
    }

    private static async Task SeedSetsAsync(AppDbContext context)
    {
        context.FlashcardSets.AddRange(
            new FlashcardSet { Id = 1, Title = "Source", UserId = "user-1" },
            new FlashcardSet { Id = 2, Title = "Owned other", UserId = "user-1" },
            new FlashcardSet { Id = 3, Title = "Foreign", UserId = "user-2" });
        await context.SaveChangesAsync();
    }

    private static Flashcard CreateCard(
        int id,
        int setId,
        string frontText,
        string backText)
    {
        return new Flashcard
        {
            Id = id,
            FlashcardSetId = setId,
            FrontText = frontText,
            BackText = backText,
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = "Example.",
            ExampleMeaning = "Example meaning.",
            OrderIndex = id
        };
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private sealed class CountingRandom : Random
    {
        private int _nextValue;
        public int NextCalls { get; private set; }

        public override int Next(int maxValue)
        {
            NextCalls++;
            return _nextValue++ % maxValue;
        }
    }
}
