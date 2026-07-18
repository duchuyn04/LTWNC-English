using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ltwnc.Tests.Services.FlashcardSets;

public class FlashcardSetServiceCardTests
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
    public async Task GetCardAsync_Owned_ReturnsCard()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);
        var card = await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, userId);

        var result = await service.GetCardAsync(card.Id, userId);

        Assert.NotNull(result);
        Assert.Equal(card.Id, result.Id);
    }

    [Fact]
    public async Task GetCardAsync_NotFound_ReturnsNull()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());

        var result = await service.GetCardAsync(9999, "user-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCardAsync_NotOwned_ThrowsUnauthorizedAccessException()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var ownerId = "user-1";
        var otherId = "user-2";
        var set = await service.CreateSetAsync("Test", null, false, ownerId);
        var card = await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, ownerId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetCardAsync(card.Id, otherId));
    }

    [Fact]
    public async Task DeleteAllCardsAsync_RemovesAllCardsAndProgress()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);
        var card1 = await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, userId);
        var card2 = await service.AddCardAsync(set.Id, "b", "2", null, null, null, null, null, null, null, false, userId);

        context.UserProgresses.Add(new UserProgress
        {
            UserId = userId,
            FlashcardId = card1.Id,
            IsLearned = true
        });
        await context.SaveChangesAsync();

        await service.DeleteAllCardsAsync(set.Id, userId);

        Assert.Empty(await context.Flashcards.Where(c => c.FlashcardSetId == set.Id).ToListAsync());
        Assert.Empty(await context.UserProgresses.Where(p => p.UserId == userId).ToListAsync());
    }

    [Fact]
    public async Task DeleteAllCardsAsync_NotOwned_ThrowsUnauthorizedAccessException()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var ownerId = "user-1";
        var otherId = "user-2";
        var set = await service.CreateSetAsync("Test", null, false, ownerId);
        await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, ownerId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteAllCardsAsync(set.Id, otherId));
    }
}
