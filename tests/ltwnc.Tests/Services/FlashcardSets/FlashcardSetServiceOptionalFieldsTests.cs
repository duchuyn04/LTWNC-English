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

    [Fact]
    public async Task UpdateCardAsync_WithEmptyOptionalFields_SavesEmptyStrings()
    {
        using var context = CreateContext();
        var service = new FlashcardSetService(context, MockEnvironment());
        var userId = "user-1";
        var set = await service.CreateSetAsync("Test", null, false, userId);

        var card = await service.AddCardAsync(
            set.Id,
            frontText: "hello",
            backText: "xin chào",
            pronunciation: "/həˈloʊ/",
            partOfSpeech: "interjection",
            exampleSentence: "Hello world",
            exampleMeaning: "Chào thế giới",
            synonyms: null,
            imageUrl: null,
            imageFile: null,
            isStarred: false,
            userId);

        await service.UpdateCardAsync(
            card.Id,
            frontText: "hi",
            backText: "chào",
            pronunciation: "",
            partOfSpeech: "",
            exampleSentence: "",
            exampleMeaning: "",
            synonyms: null,
            imageUrl: null,
            imageFile: null,
            removeUploadedImage: false,
            isStarred: false,
            userId);

        var updated = await context.Flashcards.FindAsync(card.Id);
        Assert.NotNull(updated);
        Assert.Equal("hi", updated.FrontText);
        Assert.Equal("chào", updated.BackText);
        Assert.Equal(string.Empty, updated.Pronunciation);
        Assert.Equal(string.Empty, updated.PartOfSpeech);
        Assert.Equal(string.Empty, updated.ExampleSentence);
        Assert.Equal(string.Empty, updated.ExampleMeaning);
    }

    [Fact]
    public async Task UpdateCardAsync_RemoveUploadedImage_DeletesTheStoredFile()
    {
        string webRoot = Path.Combine(Path.GetTempPath(), "ltwnc-image-cleanup", Guid.NewGuid().ToString());
        string uploadDirectory = Path.Combine(webRoot, "uploads", "flashcards");
        Directory.CreateDirectory(uploadDirectory);
        string physicalPath = Path.Combine(uploadDirectory, "old.png");
        await File.WriteAllBytesAsync(physicalPath, [1, 2, 3]);
        try
        {
            using var context = CreateContext();
            var environment = new Mock<IWebHostEnvironment>();
            environment.Setup(item => item.WebRootPath).Returns(webRoot);
            var service = new FlashcardSetService(context, environment.Object);
            var set = await service.CreateSetAsync("Test", null, false, "user-1");
            var card = await service.AddCardAsync(
                set.Id, "hello", "xin chào", null, null, null, null, null, null, null, false, "user-1");
            card.UploadedImagePath = "/uploads/flashcards/old.png";
            await context.SaveChangesAsync();

            await service.UpdateCardAsync(
                card.Id, "hello", "xin chào", null, null, null, null, null, null, null,
                removeUploadedImage: true, isStarred: false, "user-1");

            Assert.False(File.Exists(physicalPath));
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }
}
