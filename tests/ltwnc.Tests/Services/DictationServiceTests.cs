using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services;

namespace ltwnc.Tests.Services;

public class DictationServiceTests
{
    // Tạo DbContext in-memory mới cho mỗi test
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // Tạo bộ thẻ mẫu
    private async Task<FlashcardSet> SeedSetAsync(AppDbContext context)
    {
        var set = new FlashcardSet
        {
            Id = 1,
            Title = "Test Set",
            UserId = "user-1",
            IsPublic = true
        };
        await context.FlashcardSets.AddAsync(set);
        await context.SaveChangesAsync();
        return set;
    }

    // Tạo thẻ mẫu
    private async Task<Flashcard> SeedCardAsync(AppDbContext context, int id, string term, string def, string? synonyms = null)
    {
        var card = new Flashcard
        {
            Id = id,
            FlashcardSetId = 1,
            FrontText = term,
            BackText = def,
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = "Example",
            ExampleMeaning = "Ví dụ",
            Synonyms = synonyms,
            OrderIndex = id
        };
        await context.Flashcards.AddAsync(card);
        await context.SaveChangesAsync();
        return card;
    }

    [Fact]
    public async Task GetCardsForDictationAsync_StarredOnly_ReturnsStarredCards()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");
        await SeedCardAsync(context, 2, "world", "thế giớ");
        context.Flashcards.Find(2)!.IsStarred = true;
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var settings = new UserStudySettings { StarredOnly = true };

        var result = await service.GetCardsForDictationAsync(1, "user-1", settings);

        Assert.Single(result);
        Assert.Equal("world", result[0].FrontText);
    }

    [Fact]
    public async Task CheckAnswerAsync_CorrectExactAnswer_ReturnsIsCorrectTrue()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "hello", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_WrongCaseAndSpaces_Accepted()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "Hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "  HELLO  ", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_SynonymAccepted_WhenEnabled()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "big", "lớn", synonyms: "large, huge");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "large", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_SynonymRejected_WhenDisabled()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "big", "lớn", synonyms: "large");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "large", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: false);

        Assert.False(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_DefinitionMode_ChecksBackText()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "xin chào", "user-1",
            DictationAnswerMode.Definition, acceptSynonyms: false);

        Assert.True(result.IsCorrect);
        Assert.Equal("xin chào", result.CorrectAnswer);
    }

    [Fact]
    public async Task CheckAnswerAsync_WrongAnswer_UpdatesProgressAsLearning()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        await service.CheckAnswerAsync(
            session.Id, 1, "wrong", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        var progress = await context.UserProgresses.FirstAsync(p => p.UserId == "user-1" && p.FlashcardId == 1);
        Assert.False(progress.IsLearned);
        Assert.Equal(UserProgressStatus.Learning, progress.Status);
        Assert.Equal(1, progress.WrongCount);
    }

    [Fact]
    public async Task CheckAnswerAsync_CardFromOtherSet_ThrowsKeyNotFound()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var otherSet = new FlashcardSet
        {
            Id = 2,
            Title = "Other Set",
            UserId = "user-1",
            IsPublic = true
        };
        await context.FlashcardSets.AddAsync(otherSet);
        var otherCard = new Flashcard
        {
            Id = 2,
            FlashcardSetId = 2,
            FrontText = "other",
            BackText = "khác",
            Pronunciation = "/test/",
            PartOfSpeech = "noun",
            ExampleSentence = "Example",
            ExampleMeaning = "Ví dụ",
            OrderIndex = 0
        };
        await context.Flashcards.AddAsync(otherCard);
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CheckAnswerAsync(session.Id, 2, "other", "user-1", DictationAnswerMode.Term, true));
    }

    [Fact]
    public async Task CompleteSessionAsync_SetsScore()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        await service.CompleteSessionAsync(session.Id, 85);

        var completed = await context.StudySessions.FindAsync(session.Id);
        Assert.Equal(85, completed!.Score);
    }

    [Fact]
    public async Task GetSessionResultAsync_ReturnsWrongCardsOnly()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);
        await service.CheckAnswerAsync(session.Id, 1, "wrong", "user-1", DictationAnswerMode.Term, true);

        var result = await service.GetSessionResultAsync(session.Id, "user-1");

        Assert.Equal(1, result.TotalCards);
        Assert.Equal(0, result.CorrectCount);
        Assert.Single(result.WrongCards);
    }

    [Fact]
    public async Task GetCardsForDictationAsync_UnlearnedOnly_ReturnsUnlearnedCards()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");
        await SeedCardAsync(context, 2, "world", "thế giới");

        context.UserProgresses.Add(new UserProgress
        {
            UserId = "user-1",
            FlashcardId = 1,
            IsLearned = true
        });
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var settings = new UserStudySettings { UnlearnedOnly = true };

        var result = await service.GetCardsForDictationAsync(1, "user-1", settings);

        Assert.Single(result);
        Assert.Equal("world", result[0].FrontText);
    }

    [Fact]
    public async Task GetCardsForDictationAsync_StarredAndUnlearnedOnly_ReturnsIntersection()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");
        await SeedCardAsync(context, 2, "world", "thế giới");
        await SeedCardAsync(context, 3, "big", "lớn");

        context.Flashcards.Find(1)!.IsStarred = true;
        context.Flashcards.Find(2)!.IsStarred = true;
        context.Flashcards.Find(3)!.IsStarred = true;

        context.UserProgresses.Add(new UserProgress
        {
            UserId = "user-1",
            FlashcardId = 1,
            IsLearned = true
        });
        context.UserProgresses.Add(new UserProgress
        {
            UserId = "user-1",
            FlashcardId = 2,
            IsLearned = false
        });
        context.UserProgresses.Add(new UserProgress
        {
            UserId = "user-1",
            FlashcardId = 3,
            IsLearned = true
        });
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var settings = new UserStudySettings { StarredOnly = true, UnlearnedOnly = true };

        var result = await service.GetCardsForDictationAsync(1, "user-1", settings);

        Assert.Single(result);
        Assert.Equal("world", result[0].FrontText);
    }

    [Fact]
    public async Task GetCardsForDictationAsync_ShuffleTrue_ChangesOrder()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        for (int i = 1; i <= 10; i++)
        {
            await SeedCardAsync(context, i, $"term{i}", $"def{i}");
        }

        var service = new DictationService(context);
        var settings = new UserStudySettings { DictationShuffle = true };

        var result = await service.GetCardsForDictationAsync(1, "user-1", settings);

        Assert.Equal(10, result.Count);
        Assert.NotEqual(Enumerable.Range(1, 10), result.Select(c => c.Id));
    }

    [Fact]
    public async Task GetCardsForDictationAsync_ShuffleFalse_KeepsOrder()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        for (int i = 1; i <= 5; i++)
        {
            await SeedCardAsync(context, i, $"term{i}", $"def{i}");
        }

        var service = new DictationService(context);
        var settings = new UserStudySettings { DictationShuffle = false };

        var result = await service.GetCardsForDictationAsync(1, "user-1", settings);

        Assert.Equal(Enumerable.Range(1, 5), result.Select(c => c.Id));
    }

    [Fact]
    public async Task CheckAnswerAsync_DefinitionMode_SynonymsNotAccepted()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "big", "lớn", synonyms: "large");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "large", "user-1",
            DictationAnswerMode.Definition, acceptSynonyms: true);

        Assert.False(result.IsCorrect);
        Assert.Equal("lớn", result.CorrectAnswer);
    }

    [Fact]
    public async Task CheckAnswerAsync_TermMode_MultipleSynonymsAccepted()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "big", "lớn", synonyms: "large, huge, great");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "huge", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_PunctuationIgnored()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "Hello,!!", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_EmptyAnswer_ReturnsFalse()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "   ", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.False(result.IsCorrect);
    }

    [Fact]
    public async Task CheckAnswerAsync_WrongAnswer_ReturnsHint()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "wrong", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.False(result.IsCorrect);
        Assert.NotNull(result.Hint);
        Assert.Contains("IPA: /test/", result.Hint);
        Assert.Contains("Nghĩa: xin chào", result.Hint);
    }

    [Fact]
    public async Task CheckAnswerAsync_CorrectAnswer_ReturnsNullHint()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "hello", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.True(result.IsCorrect);
        Assert.Null(result.Hint);
    }

    [Fact]
    public async Task CheckAnswerAsync_CorrectAnswer_UpdatesProgressAsMastered()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        await SeedCardAsync(context, 1, "hello", "xin chào");

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync("user-1", 1);

        await service.CheckAnswerAsync(
            session.Id, 1, "hello", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        var progress = await context.UserProgresses.FirstAsync(p => p.UserId == "user-1" && p.FlashcardId == 1);
        Assert.True(progress.IsLearned);
        Assert.Equal(UserProgressStatus.Mastered, progress.Status);
        Assert.Equal(1, progress.CorrectCount);
    }

    [Fact]
    public async Task GetCardsForDictationAsync_ExampleSentenceMode_ExcludesCardsWithoutSentence()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        var missingSentence = await SeedCardAsync(context, 1, "empty", "trống");
        missingSentence.ExampleSentence = "";
        await SeedCardAsync(context, 2, "hello", "xin chào");
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var settings = new UserStudySettings
        {
            DictationContentMode = DictationContentMode.ExampleSentence
        };

        var result = await service.GetCardsForDictationAsync(1, "user-1", settings);

        Assert.Equal(2, Assert.Single(result).Id);
    }

    [Fact]
    public async Task CheckAnswerAsync_ExampleSentenceMode_ReturnsWordLevelFeedback()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        var card = await SeedCardAsync(context, 1, "hello", "xin chào");
        card.ExampleSentence = "Hello, world!";
        card.ExampleMeaning = "Xin chào, thế giới!";
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync(
            "user-1", 1, DictationContentMode.ExampleSentence);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "Helo world", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.False(result.IsCorrect);
        Assert.Equal("Xin chào, thế giới!", result.ExampleMeaning);
        Assert.Collection(result.WordComparison,
            word =>
            {
                Assert.Equal(DictationWordStatus.Incorrect, word.Status);
                Assert.Equal("Helo", word.AnsweredWord);
                Assert.Equal("Hello,", word.CorrectWord);
            },
            word =>
            {
                Assert.Equal(DictationWordStatus.Correct, word.Status);
                Assert.Equal("world", word.AnsweredWord);
                Assert.Equal("world!", word.CorrectWord);
            });
    }

    [Theory]
    [InlineData("  hello    WORLD  ", true, null)]
    [InlineData("Hello", false, DictationWordStatus.Missing)]
    [InlineData("Hello brave world", false, DictationWordStatus.Extra)]
    [InlineData("world Hello", false, DictationWordStatus.Incorrect)]
    [InlineData("", false, DictationWordStatus.Missing)]
    public async Task CheckAnswerAsync_ExampleSentenceMode_HandlesSentenceEdgeCases(
        string answer,
        bool expectedCorrect,
        DictationWordStatus? expectedDifference)
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        var card = await SeedCardAsync(context, 1, "hello", "xin chào");
        card.ExampleSentence = "Hello, world!";
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync(
            "user-1", 1, DictationContentMode.ExampleSentence);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, answer, "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.Equal(expectedCorrect, result.IsCorrect);
        if (expectedDifference is not null)
        {
            Assert.Contains(result.WordComparison, word => word.Status == expectedDifference);
        }
    }

    [Fact]
    public async Task CheckAnswerAsync_ExampleSentenceMode_EmptyAnswer_MarksAllWordsMissing()
    {
        await using var context = CreateContext();
        await SeedSetAsync(context);
        var card = await SeedCardAsync(context, 1, "hello", "xin chào");
        card.ExampleSentence = "Hello, world!";
        card.ExampleMeaning = "Xin chào, thế giới!";
        await context.SaveChangesAsync();

        var service = new DictationService(context);
        var session = await service.CreateSessionAsync(
            "user-1", 1, DictationContentMode.ExampleSentence);

        var result = await service.CheckAnswerAsync(
            session.Id, 1, "", "user-1",
            DictationAnswerMode.Term, acceptSynonyms: true);

        Assert.False(result.IsCorrect);
        Assert.Equal("Hello, world!", result.CorrectAnswer);
        Assert.Equal("Xin chào, thế giới!", result.ExampleMeaning);
        Assert.Collection(result.WordComparison,
            word =>
            {
                Assert.Equal(DictationWordStatus.Missing, word.Status);
                Assert.Equal("Hello,", word.CorrectWord);
            },
            word =>
            {
                Assert.Equal(DictationWordStatus.Missing, word.Status);
                Assert.Equal("world!", word.CorrectWord);
            });

        var progress = await context.UserProgresses.FirstAsync(p => p.UserId == "user-1" && p.FlashcardId == 1);
        Assert.False(progress.IsLearned);
        Assert.Equal(UserProgressStatus.Learning, progress.Status);
        Assert.Equal(1, progress.WrongCount);
    }
}
