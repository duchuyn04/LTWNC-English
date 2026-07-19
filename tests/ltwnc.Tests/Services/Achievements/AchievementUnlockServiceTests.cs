using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Achievements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace ltwnc.Tests.StudyEvents;

// Kiểm tra service mở khóa huy hiệu theo snapshot metric (đủ mốc, không vượt, idempotent)
public class AchievementUnlockServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AchievementUnlockService _sut;

    public AchievementUnlockServiceTests()
    {
        // Database giả trong bộ nhớ, mỗi test một tên riêng
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        var progress = new AchievementProgressService(_context);
        _sut = new AchievementUnlockService(_context, progress);
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

    // Tạo bộ thẻ dùng cho StudySession
    private async Task<FlashcardSet> SeedSetAsync(string userId, string title)
    {
        var set = new FlashcardSet
        {
            Title = title,
            UserId = userId,
            IsPublic = true
        };
        _context.FlashcardSets.Add(set);
        await _context.SaveChangesAsync();
        return set;
    }

    // 25 thẻ thuộc → mở first + 10 + 25, không mở 50
    [Fact]
    public async Task SyncEligibleAsync_unlocks_all_tiers_met_not_higher()
    {
        await SeedMasteredCardsAsync("u1", 25);

        var newly = await _sut.SyncEligibleAsync("u1");

        var codes = await _context.UserAchievements
            .Where(a => a.UserId == "u1")
            .Select(a => a.Code)
            .ToListAsync();

        Assert.Contains(AchievementCatalog.FirstCardMastered, codes);
        Assert.Contains(AchievementCatalog.CardsMastered10, codes);
        Assert.Contains(AchievementCatalog.CardsMastered25, codes);
        Assert.DoesNotContain(AchievementCatalog.CardsMastered50, codes);
        Assert.Contains(AchievementCatalog.FirstCardMastered, newly.Select(d => d.Code));
        Assert.Contains(AchievementCatalog.CardsMastered10, newly.Select(d => d.Code));
        Assert.Contains(AchievementCatalog.CardsMastered25, newly.Select(d => d.Code));
        Assert.DoesNotContain(AchievementCatalog.CardsMastered50, newly.Select(d => d.Code));
    }

    // Gọi lần hai → không mở thêm, first_card vẫn đúng 1 dòng
    [Fact]
    public async Task SyncEligibleAsync_is_idempotent()
    {
        await SeedMasteredCardsAsync("u1", 1);

        await _sut.SyncEligibleAsync("u1");
        var second = await _sut.SyncEligibleAsync("u1");

        Assert.Empty(second);
        Assert.Equal(
            1,
            await _context.UserAchievements.CountAsync(
                a => a.UserId == "u1" && a.Code == AchievementCatalog.FirstCardMastered));
    }

    // Buổi Dictation điểm 100 → mở perfect + first_dictation_session
    [Fact]
    public async Task SyncEligibleAsync_unlocks_perfect_dictation()
    {
        var set = await SeedSetAsync("u1", "Dictation set");
        _context.StudySessions.Add(new StudySession
        {
            UserId = "u1",
            FlashcardSetId = set.Id,
            Mode = StudyMode.Dictation,
            Score = 100,
            CompletedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var newly = await _sut.SyncEligibleAsync("u1");
        var codes = newly.Select(d => d.Code).ToList();

        Assert.Contains(AchievementCatalog.FirstDictationSession, codes);
        Assert.Contains(AchievementCatalog.DictationPerfectSession, codes);

        var stored = await _context.UserAchievements
            .Where(a => a.UserId == "u1")
            .Select(a => a.Code)
            .ToListAsync();
        Assert.Contains(AchievementCatalog.FirstDictationSession, stored);
        Assert.Contains(AchievementCatalog.DictationPerfectSession, stored);
    }

    [Fact]
    public async Task SyncEligibleAsync_DoesNotHideAnUnrelatedDatabaseFailure()
    {
        var root = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), root)
            .AddInterceptors(new FailingSaveInterceptor())
            .Options;
        await using var context = new AppDbContext(options);
        var progress = new Mock<IAchievementProgressService>();
        progress.Setup(service => service.GetSnapshotAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AchievementProgressSnapshot { CardsMastered = 1 });
        var service = new AchievementUnlockService(context, progress.Object);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.SyncEligibleAsync("u1"));
    }

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateException("Simulated storage failure."));
    }
}
