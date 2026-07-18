using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ltwnc.Tests.Services.FlashcardSets;

public class FlashcardSetServiceReorderTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IWebHostEnvironment MockEnvironment()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(x => x.WebRootPath).Returns(Path.GetTempPath());
        return env.Object;
    }

    [Fact]
    public async Task ReorderCardsAsync_UpdatesOrderIndex()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);

        var card1 = await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, userId);
        var card2 = await service.AddCardAsync(set.Id, "b", "2", null, null, null, null, null, null, null, false, userId);
        var card3 = await service.AddCardAsync(set.Id, "c", "3", null, null, null, null, null, null, null, false, userId);

        await service.ReorderCardsAsync(set.Id, new[] { card3.Id, card1.Id, card2.Id }, userId);

        var cards = await context.Flashcards
            .Where(c => c.FlashcardSetId == set.Id)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync();

        Assert.Equal(card3.Id, cards[0].Id);
        Assert.Equal(card1.Id, cards[1].Id);
        Assert.Equal(card2.Id, cards[2].Id);
    }

    [Fact]
    public async Task ReorderCardsAsync_MissingCard_ThrowsArgumentException()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);

        var card1 = await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, userId);
        var card2 = await service.AddCardAsync(set.Id, "b", "2", null, null, null, null, null, null, null, false, userId);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReorderCardsAsync(set.Id, new[] { card1.Id }, userId));

        Assert.Contains("Thiếu thứ tự", exception.Message);
    }

    [Fact]
    public async Task ReorderCardsAsync_UnknownCard_ThrowsArgumentException()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);

        var card1 = await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, userId);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReorderCardsAsync(set.Id, new[] { card1.Id, 9999 }, userId));

        Assert.Contains("không thuộc bộ thẻ", exception.Message);
    }

    [Fact]
    public async Task ReorderCardsAsync_DuplicateCard_ThrowsArgumentException()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);

        var card1 = await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, userId);
        var card2 = await service.AddCardAsync(set.Id, "b", "2", null, null, null, null, null, null, null, false, userId);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReorderCardsAsync(set.Id, new[] { card1.Id, card1.Id, card2.Id }, userId));

        Assert.Contains("trùng lặp", exception.Message);
    }
}
