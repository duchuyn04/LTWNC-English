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
            .ConfigureWarnings(b => b.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
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
            Description = "A public set description",
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
                Synonyms = $"syn{i}",
                ImageUrl = $"https://example.com/{i}.png",
                UploadedImagePath = $"/uploads/flashcards/{i}.png",
                IsStarred = i % 2 == 0,
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

    [Fact]
    public async Task CopyPublicSetAsync_creates_private_cards_with_new_ids()
    {
        var source = await SeedPublicSetAsync("author", "Public", cardCount: 2);
        var copy = await _service.CopyPublicSetAsync(source.Id, "learner");
        var copied = await _context.Flashcards.Where(c => c.FlashcardSetId == copy.Id).ToListAsync();

        Assert.False(copy.IsPublic);
        Assert.Equal(2, copied.Count);
        Assert.DoesNotContain(copied, c => source.Flashcards.Any(sourceCard => sourceCard.Id == c.Id));
    }

    [Fact]
    public async Task CopyPublicSetAsync_preserves_content_and_clears_uploaded_image_path()
    {
        var source = await SeedPublicSetAsync("author", "Public", cardCount: 2);
        var copy = await _service.CopyPublicSetAsync(source.Id, "learner");
        var copied = await _context.Flashcards.Where(c => c.FlashcardSetId == copy.Id).OrderBy(c => c.OrderIndex).ToListAsync();

        Assert.Equal(source.Title, copy.Title);
        Assert.Equal(source.Description, copy.Description);
        Assert.Equal(source.Id, copy.SourceSetId);
        Assert.Equal("learner", copy.UserId);
        Assert.False(copy.IsPublic);

        Assert.Equal(2, copied.Count);
        var sourceCards = source.Flashcards.OrderBy(c => c.OrderIndex).ToList();
        for (var i = 0; i < copied.Count; i++)
        {
            Assert.Equal(sourceCards[i].FrontText, copied[i].FrontText);
            Assert.Equal(sourceCards[i].BackText, copied[i].BackText);
            Assert.Equal(sourceCards[i].Pronunciation, copied[i].Pronunciation);
            Assert.Equal(sourceCards[i].PartOfSpeech, copied[i].PartOfSpeech);
            Assert.Equal(sourceCards[i].ExampleSentence, copied[i].ExampleSentence);
            Assert.Equal(sourceCards[i].ExampleMeaning, copied[i].ExampleMeaning);
            Assert.Equal(sourceCards[i].Synonyms, copied[i].Synonyms);
            Assert.Equal(sourceCards[i].ImageUrl, copied[i].ImageUrl);
            Assert.Null(copied[i].UploadedImagePath);
            Assert.False(copied[i].IsStarred);
            Assert.Equal(sourceCards[i].OrderIndex, copied[i].OrderIndex);
        }
    }

    [Fact]
    public async Task CopyPublicSetAsync_resets_IsStarred_for_all_cards()
    {
        var source = await SeedPublicSetAsync("author", "Public", cardCount: 3);
        foreach (var card in source.Flashcards)
            card.IsStarred = true;
        await _context.SaveChangesAsync();

        var copy = await _service.CopyPublicSetAsync(source.Id, "learner");
        var copied = await _context.Flashcards
            .Where(c => c.FlashcardSetId == copy.Id)
            .ToListAsync();

        Assert.All(copied, c => Assert.False(c.IsStarred));
    }
}
