using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyModes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests.StudyModes;

// Kiểm tra FlashcardModeStrategy: lấy thẻ đúng bộ, lọc sao/chưa biết, sắp xếp, xây dựng option
public class FlashcardModeStrategyTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static FlashcardModeStrategy CreateStrategy(AppDbContext context)
    {
        return new FlashcardModeStrategy(new StudyCardQueryService(context));
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
            new Flashcard { Id = 1, FlashcardSetId = 1, FrontText = "a", BackText = "1", OrderIndex = 2, IsStarred = false },
            new Flashcard { Id = 2, FlashcardSetId = 1, FrontText = "b", BackText = "2", OrderIndex = 0, IsStarred = true },
            new Flashcard { Id = 3, FlashcardSetId = 1, FrontText = "c", BackText = "3", OrderIndex = 1, IsStarred = false }
        );

        await context.SaveChangesAsync();
    }

    [Fact]
    // Chỉ trả về thẻ thuộc đúng bộ thẻ
    public async Task GetCardsAsync_returns_only_cards_in_set()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);
        context.Flashcards.Add(new Flashcard { Id = 4, FlashcardSetId = 99, FrontText = "other", BackText = "x", OrderIndex = 0 });
        await context.SaveChangesAsync();

        var strategy = CreateStrategy(context);
        var cards = await strategy.GetCardsAsync(1, new UserStudySettings(), "user-1");

        Assert.Equal(3, cards.Count);
        Assert.All(cards, c => Assert.Equal(1, c.FlashcardSetId));
    }

    [Fact]
    // Thẻ sắp xếp theo OrderIndex
    public async Task GetCardsAsync_orders_by_OrderIndex()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);

        var strategy = CreateStrategy(context);
        var cards = await strategy.GetCardsAsync(1, new UserStudySettings(), "user-1");

        Assert.Equal(new[] { 2, 3, 1 }, cards.Select(c => c.Id));
    }

    [Fact]
    // Lọc chỉ lấy thẻ đã đánh sao
    public async Task GetCardsAsync_starred_only_filter_returns_starred_cards()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);

        var strategy = CreateStrategy(context);
        var cards = await strategy.GetCardsAsync(1, new UserStudySettings { StarredOnly = true }, "user-1");

        var single = Assert.Single(cards);
        Assert.Equal(2, single.Id);
        Assert.True(single.IsStarred);
    }

    [Fact]
    // Lọc chỉ lấy thẻ chưa biết, loại thẻ đã biết
    public async Task GetCardsAsync_unlearned_only_filter_excludes_learned_cards()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);
        context.UserProgresses.Add(new UserProgress { UserId = "user-1", FlashcardId = 1, IsLearned = true });
        await context.SaveChangesAsync();

        var strategy = CreateStrategy(context);
        var cards = await strategy.GetCardsAsync(1, new UserStudySettings { UnlearnedOnly = true }, "user-1");

        Assert.Equal(2, cards.Count);
        Assert.DoesNotContain(cards, c => c.Id == 1);
    }

    [Fact]
    // User ẩn danh không áp dụng bộ lọc UnlearnedOnly
    public async Task GetCardsAsync_null_userId_does_not_apply_unlearned_filter()
    {
        await using var context = CreateContext();
        await SeedSetAndCardsAsync(context);

        var strategy = CreateStrategy(context);
        var cards = await strategy.GetCardsAsync(1, new UserStudySettings { UnlearnedOnly = true }, userId: null);

        Assert.Equal(3, cards.Count);
    }

    [Fact]
    // BuildOption trả về thông tin đúng khi có thẻ
    public void BuildOption_returns_correct_values()
    {
        using var context = CreateContext();
        var strategy = CreateStrategy(context);
        var cards = new List<Flashcard>
        {
            new() { Id = 1, FlashcardSetId = 1, FrontText = "a", BackText = "1" },
            new() { Id = 2, FlashcardSetId = 1, FrontText = "b", BackText = "2" }
        };

        var option = strategy.BuildOption(1, cards, new UserStudySettings());

        Assert.Equal(StudyMode.Flashcard, option.Mode);
        Assert.Equal("Flashcard", option.Name);
        Assert.Equal("/Study/1/Flashcard", option.ActionUrl);
        Assert.True(option.IsAvailable);
        Assert.Equal(2, option.CardCount);
        Assert.Equal(30, option.EstimatedSeconds);
    }

    [Fact]
    // BuildOption đánh dấu không khả dụng khi bộ thẻ rỗng
    public void BuildOption_with_no_cards_is_unavailable()
    {
        using var context = CreateContext();
        var strategy = CreateStrategy(context);

        var option = strategy.BuildOption(1, Array.Empty<Flashcard>(), new UserStudySettings());

        Assert.False(option.IsAvailable);
        Assert.Equal(0, option.CardCount);
        Assert.Equal(0, option.EstimatedSeconds);
    }
}
