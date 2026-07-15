using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Achievements;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.Services;

// Kiểm tra AchievementService.GetPageAsync: progress bar + rescan mở khóa
public class AchievementServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AchievementService _sut;

    public AchievementServiceTests()
    {
        // Database giả trong bộ nhớ, mỗi test một tên riêng
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        var progress = new AchievementProgressService(_context);
        var unlock = new AchievementUnlockService(_context, progress);
        _sut = new AchievementService(_context, unlock, progress);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    // Tạo bộ thẻ + N thẻ đã thuộc cho user
    private async Task SeedMasteredCardsAsync(string userId, int count)
    {
        var set = new FlashcardSet
        {
            Title = $"Set-{userId}",
            UserId = userId,
            IsPublic = true
        };
        _context.FlashcardSets.Add(set);
        await _context.SaveChangesAsync();

        for (var i = 0; i < count; i++)
        {
            var card = new Flashcard
            {
                FlashcardSetId = set.Id,
                FrontText = $"front-{userId}-{i}",
                BackText = $"back-{userId}-{i}",
                Pronunciation = "/a/",
                PartOfSpeech = "noun",
                ExampleSentence = "ex",
                ExampleMeaning = "nghĩa",
                OrderIndex = i
            };
            _context.Flashcards.Add(card);
            await _context.SaveChangesAsync();

            _context.UserProgresses.Add(new UserProgress
            {
                UserId = userId,
                FlashcardId = card.Id,
                IsLearned = true,
                Status = UserProgressStatus.Mastered
            });
        }

        await _context.SaveChangesAsync();
    }

    // 7 thẻ thuộc, chưa có UserAchievement → cards_mastered_10 vẫn khóa, 70%
    [Fact]
    public async Task GetPageAsync_includes_progress_for_locked_badge()
    {
        await SeedMasteredCardsAsync("u1", 7);

        var page = await _sut.GetPageAsync("u1");
        var ten = page.Items.Single(i => i.Code == AchievementCatalog.CardsMastered10);

        Assert.False(ten.IsUnlocked);
        Assert.Equal(7, ten.Current);
        Assert.Equal(10, ten.Target);
        Assert.Equal(70, ten.ProgressPercent);
        Assert.Equal("/Set", ten.CtaUrl);
    }

    // 10 thẻ thuộc, zero rows → rescan ghi DB + NewlyUnlockedTitles có tiêu đề
    [Fact]
    public async Task GetPageAsync_rescan_unlocks_and_reports_new_titles()
    {
        await SeedMasteredCardsAsync("u1", 10);

        Assert.Equal(0, await _context.UserAchievements.CountAsync(a => a.UserId == "u1"));

        var page = await _sut.GetPageAsync("u1");

        var rows = await _context.UserAchievements
            .Where(a => a.UserId == "u1")
            .Select(a => a.Code)
            .ToListAsync();

        Assert.NotEmpty(rows);
        Assert.Contains(AchievementCatalog.FirstCardMastered, rows);
        Assert.Contains(AchievementCatalog.CardsMastered10, rows);
        Assert.NotEmpty(page.NewlyUnlockedTitles);

        var ten = page.Items.Single(i => i.Code == AchievementCatalog.CardsMastered10);
        Assert.True(ten.IsUnlocked);
        Assert.Equal(10, ten.Current);
        Assert.Equal(100, ten.ProgressPercent);
        Assert.Contains(ten.Title, page.NewlyUnlockedTitles);
    }
}
