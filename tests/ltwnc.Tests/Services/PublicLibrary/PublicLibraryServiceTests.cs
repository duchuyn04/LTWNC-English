using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.PublicLibrary;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Tests.Services.PublicLibrary;

// Kiểm tra truy vấn thư viện công khai (in-memory): lọc visibility, tìm kiếm, sắp xếp, phân trang.
public class PublicLibraryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PublicLibraryService _service;

    public PublicLibraryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new PublicLibraryService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BrowseAsync_ExcludesPrivateAndQuarantinedSetsFromItemsAndSummary()
    {
        await SeedUserAsync("author", "minhanh");
        FlashcardSet visible = await SeedSetAsync("author", "Visible", isPublic: true);
        await SeedCardsAsync(visible.Id, 2);
        await SeedSetAsync("author", "Private", isPublic: false);
        await SeedSetAsync("author", "Quarantined", isPublic: true,
            moderationStatus: FlashcardSetModerationStatus.Quarantined);
        await SeedCopyAsync(visible.Id, "learner");

        PublicLibraryResult result = await _service.BrowseAsync(new(null, "popular", 1));

        PublicLibrarySetItem item = Assert.Single(result.Items);
        Assert.Equal("Visible", item.Title);
        Assert.Equal("minhanh", item.AuthorName);
        Assert.Equal(2, item.CardCount);
        Assert.Equal(1, item.CopyCount);
        Assert.Equal(new PublicLibrarySummary(1, 2, 1), result.Summary);
    }

    [Theory]
    [InlineData("academic", "Academic words")]
    [InlineData("writing", "Academic words")]
    [InlineData("minhanh", "Academic words")]
    public async Task BrowseAsync_SearchesTitleDescriptionAndAuthorCaseInsensitively(
        string search,
        string expectedTitle)
    {
        await SeedUserAsync("author", "MinhAnh");
        await SeedUserAsync("author-2", "HoangNam");
        await SeedSetAsync("author", expectedTitle, true, description: "IELTS Writing vocabulary");
        await SeedSetAsync("author-2", "Travel", true, description: "Airport phrases");

        PublicLibraryResult result = await _service.BrowseAsync(new(search.ToUpperInvariant(), "recent", 1));

        Assert.Equal(expectedTitle, Assert.Single(result.Items).Title);
        Assert.Equal(search.ToLowerInvariant(), result.Search);
    }

    [Fact]
    public async Task BrowseAsync_NormalizesSortAndClampsPageToLastPage()
    {
        await SeedUserAsync("author", "author");
        for (int index = 0; index < 13; index++)
        {
            await SeedSetAsync("author", $"Set {index:00}", true,
                updatedAt: new DateTime(2026, 7, 1).AddDays(index));
        }

        PublicLibraryResult result = await _service.BrowseAsync(new(null, "invalid", 99));

        Assert.Equal("popular", result.Sort);
        Assert.Equal(2, result.Page);
        Assert.Equal(12, result.PageSize);
        Assert.Equal(13, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
    }

    [Theory]
    [InlineData("popular", "Popular,Large,Recent")]
    [InlineData("recent", "Recent,Large,Popular")]
    [InlineData("cards", "Large,Recent,Popular")]
    public async Task BrowseAsync_AppliesDeterministicSort(string sort, string expected)
    {
        await SeedUserAsync("author", "author");
        FlashcardSet popular = await SeedSetAsync("author", "Popular", true,
            updatedAt: new DateTime(2026, 7, 1));
        FlashcardSet large = await SeedSetAsync("author", "Large", true,
            updatedAt: new DateTime(2026, 7, 10));
        FlashcardSet recent = await SeedSetAsync("author", "Recent", true,
            updatedAt: new DateTime(2026, 7, 20));
        await SeedCardsAsync(popular.Id, 1);
        await SeedCardsAsync(large.Id, 5);
        await SeedCardsAsync(recent.Id, 1);
        await SeedCopyAsync(popular.Id, "copy-1");
        await SeedCopyAsync(popular.Id, "copy-2");
        await SeedCopyAsync(popular.Id, "copy-3");
        await SeedCopyAsync(large.Id, "copy-4");

        PublicLibraryResult result = await _service.BrowseAsync(new(null, sort, 1));

        Assert.Equal(expected.Split(','), result.Items.Select(item => item.Title));
    }

    private async Task SeedUserAsync(string id, string userName)
    {
        _db.Users.Add(new IdentityUser { Id = id, UserName = userName });
        await _db.SaveChangesAsync();
    }

    private async Task<FlashcardSet> SeedSetAsync(
        string userId,
        string title,
        bool isPublic,
        string? description = null,
        string moderationStatus = FlashcardSetModerationStatus.Active,
        DateTime? updatedAt = null)
    {
        var set = new FlashcardSet
        {
            UserId = userId,
            Title = title,
            Description = description,
            IsPublic = isPublic,
            ModerationStatus = moderationStatus,
            CreatedAt = updatedAt ?? new DateTime(2026, 7, 1),
            UpdatedAt = updatedAt ?? new DateTime(2026, 7, 1)
        };
        _db.FlashcardSets.Add(set);
        await _db.SaveChangesAsync();
        return set;
    }

    private async Task SeedCardsAsync(int setId, int count)
    {
        for (int index = 0; index < count; index++)
        {
            _db.Flashcards.Add(new Flashcard
            {
                FlashcardSetId = setId,
                FrontText = $"Front {setId}-{index}",
                BackText = $"Back {setId}-{index}",
                OrderIndex = index
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedCopyAsync(int sourceSetId, string userId)
    {
        _db.FlashcardSets.Add(new FlashcardSet
        {
            UserId = userId,
            Title = $"Copy {userId}",
            IsPublic = false,
            SourceSetId = sourceSetId
        });
        await _db.SaveChangesAsync();
    }
}
