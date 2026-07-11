using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ltwnc.Tests;

public class FlashcardSetCopySqliteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly FlashcardSetService _service;
    private readonly IdentityUser _owner;
    private const string OwnerId = "author";
    private const string LearnerId = "learner";

    public FlashcardSetCopySqliteTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        _owner = new IdentityUser
        {
            Id = OwnerId,
            UserName = OwnerId,
            NormalizedUserName = OwnerId.ToUpperInvariant()
        };
        var learner = new IdentityUser
        {
            Id = LearnerId,
            UserName = LearnerId,
            NormalizedUserName = LearnerId.ToUpperInvariant()
        };
        _context.Users.AddRange(_owner, learner);
        _context.SaveChanges();

        _service = new FlashcardSetService(_context, null!);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
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
                OrderIndex = i
            });
        }

        if (cardCount > 0)
            await _context.SaveChangesAsync();

        return set;
    }

    [Fact]
    public async Task CopyPublicSetAsync_creates_private_copy_in_sqlite()
    {
        var source = await SeedPublicSetAsync(OwnerId, "Public", cardCount: 2);

        var copy = await _service.CopyPublicSetAsync(source.Id, LearnerId);

        Assert.False(copy.IsPublic);
        Assert.Equal(LearnerId, copy.UserId);
        Assert.Equal(source.Id, copy.SourceSetId);

        var copiedCards = await _context.Flashcards
            .Where(c => c.FlashcardSetId == copy.Id)
            .ToListAsync();
        Assert.Equal(2, copiedCards.Count);
    }

    [Fact]
    public async Task CopyPublicSetAsync_returns_existing_copy_without_duplicate_in_sqlite()
    {
        var source = await SeedPublicSetAsync(OwnerId, "Public", cardCount: 1);

        var first = await _service.CopyPublicSetAsync(source.Id, LearnerId);
        var second = await _service.CopyPublicSetAsync(source.Id, LearnerId);

        Assert.Equal(first.Id, second.Id);

        var copies = await _context.FlashcardSets
            .Where(s => s.UserId == LearnerId && s.SourceSetId == source.Id)
            .ToListAsync();
        Assert.Single(copies);
    }

    [Fact]
    public async Task Copy_survives_source_deletion_because_SourceSetId_is_not_foreign_key()
    {
        var source = await SeedPublicSetAsync(OwnerId, "Public", cardCount: 1);
        var copy = await _service.CopyPublicSetAsync(source.Id, LearnerId);

        _context.FlashcardSets.Remove(source);
        await _context.SaveChangesAsync();

        var survived = await _context.FlashcardSets.FindAsync(copy.Id);
        Assert.NotNull(survived);
        Assert.Equal(source.Id, survived.SourceSetId);
    }

    [Fact]
    public async Task Unique_index_prevents_two_copies_for_same_learner_in_sqlite()
    {
        var source = await SeedPublicSetAsync(OwnerId, "Public");

        var first = new FlashcardSet
        {
            Title = source.Title,
            UserId = LearnerId,
            SourceSetId = source.Id,
            IsPublic = false
        };
        _context.FlashcardSets.Add(first);
        await _context.SaveChangesAsync();

        var duplicate = new FlashcardSet
        {
            Title = source.Title,
            UserId = LearnerId,
            SourceSetId = source.Id,
            IsPublic = false
        };
        _context.FlashcardSets.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }
}
