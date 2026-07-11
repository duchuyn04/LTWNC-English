using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests.Services;

public class FlashcardSetServiceAddCardTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly FlashcardSetService _service;

    public FlashcardSetServiceAddCardTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _service = new FlashcardSetService(_context, null!);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AddCardAsync_first_card_has_OrderIndex_zero()
    {
        var set = new FlashcardSet
        {
            Title = "Set",
            UserId = "user",
            IsPublic = true
        };
        _context.FlashcardSets.Add(set);
        await _context.SaveChangesAsync();

        var card = await _service.AddCardAsync(
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
            userId: "user");

        Assert.Equal(0, card.OrderIndex);
    }

    [Fact]
    public async Task AddCardAsync_second_card_increments_OrderIndex()
    {
        var set = new FlashcardSet
        {
            Title = "Set",
            UserId = "user",
            IsPublic = true
        };
        _context.FlashcardSets.Add(set);
        await _context.SaveChangesAsync();

        await _service.AddCardAsync(set.Id, "a", "a", "/a/", "noun", "A", "A", null, null, null, false, "user");
        var second = await _service.AddCardAsync(set.Id, "b", "b", "/b/", "noun", "B", "B", null, null, null, false, "user");

        Assert.Equal(1, second.OrderIndex);
    }
}
