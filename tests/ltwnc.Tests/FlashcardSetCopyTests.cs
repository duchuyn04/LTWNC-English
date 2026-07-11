using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests;

public class FlashcardSetCopyTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly FlashcardSetService _service;

    public FlashcardSetCopyTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _service = new FlashcardSetService(_context, null!);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<FlashcardSet> SeedPublicSetAsync(string userId, string title, int cardCount = 0)
    {
        var set = new FlashcardSet
        {
            Title = title,
            Description = null,
            UserId = userId,
            IsPublic = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.FlashcardSets.Add(set);
        await _context.SaveChangesAsync();

        for (var i = 0; i < cardCount; i++)
        {
            _context.Flashcards.Add(new Flashcard
            {
                FlashcardSetId = set.Id,
                FrontText = $"Front {i}",
                BackText = $"Back {i}",
                Pronunciation = $"/a{i}/",
                PartOfSpeech = "noun",
                ExampleSentence = $"Example {i}",
                ExampleMeaning = $"Meaning {i}",
                OrderIndex = i
            });
        }

        if (cardCount > 0)
            await _context.SaveChangesAsync();

        return await _context.FlashcardSets
            .Include(s => s.Flashcards)
            .FirstAsync(s => s.Id == set.Id);
    }

    [Fact]
    public async Task CopyPublicSetAsync_returns_the_existing_copy_for_the_same_learner()
    {
        var source = await SeedPublicSetAsync("author", "Public");
        var first = await _service.CopyPublicSetAsync(source.Id, "learner");
        var second = await _service.CopyPublicSetAsync(source.Id, "learner");

        Assert.Equal(first.Id, second.Id);
        Assert.False(first.IsPublic);
        Assert.Equal(source.Id, first.SourceSetId);
    }
}
