using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Study;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests.StudyModes;

// Kiểm tra DictationModeStrategy: lọc thẻ theo nội dung, bộ lọc sao/chưa biết, thứ tự hiển thị
public class DictationModeStrategyTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DictationModeStrategy CreateStrategy(AppDbContext context)
    {
        return new DictationModeStrategy(new StudyCardQueryService(context));
    }

    private static async Task SeedSetAndCardsAsync(AppDbContext context)
    {
        context.FlashcardSets.Add(new FlashcardSet
        {
            Id = 1,
            Title = "Set",
            UserId = "user-1",
            IsPublic = true
        });

        context.Flashcards.AddRange(
            new Flashcard { Id = 1, FlashcardSetId = 1, FrontText = "a", BackText = "1", OrderIndex = 2, IsStarred = true, ExampleSentence = "" },
            new Flashcard { Id = 2, FlashcardSetId = 1, FrontText = "b", BackText = "2", OrderIndex = 0, IsStarred = false, ExampleSentence = "Example B." },
            new Flashcard { Id = 3, FlashcardSetId = 1, FrontText = "c", BackText = "3", OrderIndex = 1, IsStarred = true, ExampleSentence = "Example C." }
        );

        await context.SaveChangesAsync();
    }

    [Fact]
    // Chế độ từ vựng lấy tất cả thẻ, kể cả không có câu ví dụ
    public async Task GetCardsAsync_vocabulary_mode_includes_cards_without_example_sentence()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);

        var strategy = CreateStrategy(context);
        var settings = new UserStudySettings { DictationContentMode = DictationContentMode.Vocabulary };
        var cards = await strategy.GetCardsAsync(1, settings, "user-1");

        Assert.Equal(3, cards.Count);
    }

    [Fact]
    // Chế độ câu ví dụ chỉ lấy thẻ có câu ví dụ
    public async Task GetCardsAsync_example_sentence_mode_excludes_cards_without_example_sentence()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);

        var strategy = CreateStrategy(context);
        var settings = new UserStudySettings { DictationContentMode = DictationContentMode.ExampleSentence };
        var cards = await strategy.GetCardsAsync(1, settings, "user-1");

        Assert.Equal(2, cards.Count);
        Assert.DoesNotContain(cards, c => c.Id == 1);
    }

    [Fact]
    // Lọc chỉ lấy thẻ đã đánh sao
    public async Task GetCardsAsync_applies_starred_only_filter()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);

        var strategy = CreateStrategy(context);
        var settings = new UserStudySettings
        {
            DictationContentMode = DictationContentMode.Vocabulary,
            StarredOnly = true
        };
        var cards = await strategy.GetCardsAsync(1, settings, "user-1");

        Assert.Equal(2, cards.Count);
        Assert.All(cards, c => Assert.True(c.IsStarred));
    }

    [Fact]
    // Lọc chỉ lấy thẻ chưa biết
    public async Task GetCardsAsync_applies_unlearned_only_filter()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);
        context.UserProgresses.Add(new UserProgress { UserId = "user-1", FlashcardId = 2, IsLearned = true });
        await context.SaveChangesAsync();

        var strategy = CreateStrategy(context);
        var settings = new UserStudySettings
        {
            DictationContentMode = DictationContentMode.ExampleSentence,
            UnlearnedOnly = true
        };
        var cards = await strategy.GetCardsAsync(1, settings, "user-1");

        Assert.Single(cards);
        Assert.Equal(3, cards[0].Id);
    }

    [Fact]
    // Thẻ trả về được sắp xếp theo OrderIndex
    public async Task GetCardsAsync_orders_by_OrderIndex()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);

        var strategy = CreateStrategy(context);
        var settings = new UserStudySettings { DictationContentMode = DictationContentMode.Vocabulary };
        var cards = await strategy.GetCardsAsync(1, settings, "user-1");

        Assert.Equal(new[] { 2, 3, 1 }, cards.Select(c => c.Id));
    }

    [Fact]
    // Không có thẻ phù hợp trong chế độ từ vựng: hiển thị lý do chung
    public void BuildOption_vocabulary_mode_shows_generic_unavailable_reason()
    {
        using var context = CreateContext();
        var strategy = CreateStrategy(context);
        var settings = new UserStudySettings { DictationContentMode = DictationContentMode.Vocabulary };

        var option = strategy.BuildOption(1, Array.Empty<Flashcard>(), settings);

        Assert.False(option.IsAvailable);
        Assert.Equal("Không có thẻ phù hợp với bộ lọc hiện tại.", option.UnavailableReason);
    }

    [Fact]
    // Không có thẻ phù hợp trong chế độ câu ví dụ: hiển thị lý do riêng
    public void BuildOption_example_sentence_mode_shows_example_unavailable_reason()
    {
        using var context = CreateContext();
        var strategy = CreateStrategy(context);
        var settings = new UserStudySettings { DictationContentMode = DictationContentMode.ExampleSentence };

        var option = strategy.BuildOption(1, Array.Empty<Flashcard>(), settings);

        Assert.False(option.IsAvailable);
        Assert.Equal("Không có thẻ có câu ví dụ phù hợp.", option.UnavailableReason);
    }

    [Fact]
    // DictationService và DictationModeStrategy phải trả về cùng tập thẻ với cùng settings
    public async Task DictationService_and_strategy_return_same_card_ids_for_same_settings()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);

        var queryService = new StudyCardQueryService(context);
        var strategies = new List<IStudyModeStrategy>
        {
            new FlashcardModeStrategy(queryService),
            new DictationModeStrategy(queryService)
        };
        var resolver = new StudyModeStrategyResolver(strategies);
        var dictationService = new DictationService(context, resolver, TestStudyEvents.NoOpPublisher());

        var settings = new UserStudySettings
        {
            DictationContentMode = DictationContentMode.ExampleSentence,
            StarredOnly = true
        };

        var strategyCards = await CreateStrategy(context).GetCardsAsync(1, settings, "user-1");
        var serviceCards = await dictationService.GetCardsForDictationAsync(1, "user-1", settings);

        Assert.Equal(
            strategyCards.OrderBy(c => c.Id).Select(c => c.Id),
            serviceCards.OrderBy(c => c.Id).Select(c => c.Id));
    }
}
