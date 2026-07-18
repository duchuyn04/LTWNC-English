using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Auth;
using ltwnc.Services.FlashcardSets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ltwnc.Tests.Services.FlashcardSets;

public class FlashcardSetServiceOptionalFieldsTests
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
    public async Task AddCardAsync_WithEmptyOptionalFields_SavesEmptyStrings()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);

        var card = await service.AddCardAsync(
            set.Id,
            frontText: "hello",
            backText: "xin chào",
            pronunciation: "",
            partOfSpeech: "",
            exampleSentence: "",
            exampleMeaning: "",
            synonyms: null,
            imageUrl: null,
            imageFile: null,
            isStarred: false,
            userId);

        Assert.Equal("hello", card.FrontText);
        Assert.Equal("xin chào", card.BackText);
        Assert.Equal(string.Empty, card.Pronunciation);
        Assert.Equal(string.Empty, card.PartOfSpeech);
        Assert.Equal(string.Empty, card.ExampleSentence);
        Assert.Equal(string.Empty, card.ExampleMeaning);
    }
}
