using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
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
        var session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = set.Id,
            Mode = StudyMode.Dictation
        };
        context.StudySessions.Add(session);
        await context.SaveChangesAsync();
        context.DictationSessionDetails.Add(new DictationSessionDetail
        {
            StudySessionId = session.Id,
            FlashcardId = card1.Id,
            AnsweredText = "a"
        });
        var mission = new ltwnc.Models.Entities.EnglishMission
        {
            StudySessionId = session.Id,
            Topic = "travel",
            Title = "Trip",
            Situation = "Station",
            NpcName = "Alex",
            NpcRole = "Clerk",
            OpeningLine = "Hello"
        };
        context.EnglishMissions.Add(mission);
        await context.SaveChangesAsync();
        context.EnglishMissionTargetWords.Add(new EnglishMissionTargetWord
        {
            EnglishMissionId = mission.Id,
            FlashcardId = card2.Id,
            Term = card2.FrontText,
            Definition = card2.BackText
        });
        await context.SaveChangesAsync();

        await service.DeleteAllCardsAsync(set.Id, userId);

        Assert.Empty(await context.Flashcards.Where(c => c.FlashcardSetId == set.Id).ToListAsync());
        Assert.Empty(await context.UserProgresses.Where(p => p.UserId == userId).ToListAsync());
        Assert.Empty(await context.DictationSessionDetails.ToListAsync());
        Assert.Empty(await context.EnglishMissionTargetWords.ToListAsync());
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

    [Fact]
    public async Task BatchImportCardsAsync_ReplaceAll_RemovesOldCardsAndProgress()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);
        var oldCard = await service.AddCardAsync(set.Id, "old", "cũ", null, null, null, null, null, null, null, false, userId);
        context.UserProgresses.Add(new UserProgress { UserId = userId, FlashcardId = oldCard.Id, IsLearned = true });
        await context.SaveChangesAsync();

        var created = await service.BatchImportCardsAsync(set.Id, new[]
        {
            new BatchImportCardItem { FrontText = "new1", BackText = "mới 1" },
            new BatchImportCardItem { FrontText = "new2", BackText = "mới 2" }
        }, replaceAll: true, userId);

        var remaining = await context.Flashcards
            .Where(c => c.FlashcardSetId == set.Id)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.Equal(new[] { "new1", "new2" }, remaining.Select(c => c.FrontText).ToArray());
        Assert.Equal(new[] { 0, 1 }, remaining.Select(c => c.OrderIndex).ToArray());
        Assert.Empty(await context.UserProgresses.Where(p => p.UserId == userId).ToListAsync());
        Assert.Equal(2, created.Count);
    }

    [Fact]
    public async Task BatchImportCardsAsync_Append_ContinuesOrderIndex()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);
        await service.AddCardAsync(set.Id, "a", "1", null, null, null, null, null, null, null, false, userId);

        await service.BatchImportCardsAsync(set.Id, new[]
        {
            new BatchImportCardItem { FrontText = "b", BackText = "2" }
        }, replaceAll: false, userId);

        var cards = await context.Flashcards
            .Where(c => c.FlashcardSetId == set.Id)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync();
        Assert.Equal(2, cards.Count);
        Assert.Equal("b", cards[1].FrontText);
        Assert.Equal(1, cards[1].OrderIndex);
    }

    [Fact]
    public async Task BatchImportCardsAsync_InvalidItem_KeepsOldCardsIntact()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);
        await service.AddCardAsync(set.Id, "old", "cũ", null, null, null, null, null, null, null, false, userId);

        await Assert.ThrowsAsync<ArgumentException>(() => service.BatchImportCardsAsync(set.Id, new[]
        {
            new BatchImportCardItem { FrontText = " ", BackText = "thiếu thuật ngữ" }
        }, replaceAll: true, userId));

        var remaining = await context.Flashcards.Where(c => c.FlashcardSetId == set.Id).ToListAsync();
        Assert.Single(remaining);
        Assert.Equal("old", remaining[0].FrontText);
    }

    [Fact]
    public async Task BatchImportCardsAsync_NotOwned_ThrowsUnauthorizedAccessException()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var ownerId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, ownerId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BatchImportCardsAsync(
            set.Id,
            new[] { new BatchImportCardItem { FrontText = "a", BackText = "1" } },
            replaceAll: false,
            "user-2"));
    }
}
