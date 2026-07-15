using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Achievements;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.StudyEvents;

// Kiểm tra observer thành tích (thin) ủy quyền unlock service mở đúng huy hiệu
public class AchievementStudyObserverTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AchievementStudyObserver _observer;

    public AchievementStudyObserverTests()
    {
        // Database giả trong bộ nhớ, mỗi test một tên riêng
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        // Observer chỉ inject UnlockService; Progress + context dùng chung InMemory
        var progress = new AchievementProgressService(_context);
        var unlock = new AchievementUnlockService(_context, progress);
        _observer = new AchievementStudyObserver(unlock);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    // Chuẩn bị: user đã có đúng N thẻ "đã thuộc" trong database
    private async Task SeedMasteredCardsAsync(string userId, int count)
    {
        var set = new FlashcardSet
        {
            Title = "Set",
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
                FrontText = $"front-{i}",
                BackText = $"back-{i}",
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

    // Tạo bộ thẻ dùng cho StudySession (metric session đọc từ DB)
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

    // Đánh dấu thuộc thẻ đầu tiên → mở huy hiệu first_card_mastered
    [Fact]
    public async Task CardProgress_learned_unlocks_first_card_mastered()
    {
        await SeedMasteredCardsAsync("u1", 1);

        await _observer.OnStudyEventAsync(new CardProgressChangedEvent(
            "u1",
            DateTime.UtcNow,
            SetId: 1,
            FlashcardId: 1,
            IsLearned: true,
            Status: UserProgressStatus.Mastered));

        var unlocked = await _context.UserAchievements
            .Where(a => a.UserId == "u1")
            .Select(a => a.Code)
            .ToListAsync();

        Assert.Contains(AchievementCatalog.FirstCardMastered, unlocked);
        Assert.DoesNotContain(AchievementCatalog.CardsMastered10, unlocked);
    }

    // Gọi lại sự kiện cùng user → không tạo thêm dòng huy hiệu trùng
    [Fact]
    public async Task Same_achievement_is_not_unlocked_twice()
    {
        await SeedMasteredCardsAsync("u1", 1);

        var studyEvent = new CardProgressChangedEvent(
            "u1",
            DateTime.UtcNow,
            SetId: 1,
            FlashcardId: 1,
            IsLearned: true,
            Status: UserProgressStatus.Mastered);

        await _observer.OnStudyEventAsync(studyEvent);
        await _observer.OnStudyEventAsync(studyEvent);

        var count = await _context.UserAchievements
            .CountAsync(a => a.UserId == "u1" && a.Code == AchievementCatalog.FirstCardMastered);

        Assert.Equal(1, count);
    }

    // Hoàn thành buổi Flashcard → mở huy hiệu first_flashcard_session
    [Fact]
    public async Task Flashcard_session_completed_unlocks_first_flashcard_session()
    {
        // Progress service đếm từ StudySessions trong DB, không từ event
        var set = await SeedSetAsync("u2", "Flash set");
        _context.StudySessions.Add(new StudySession
        {
            UserId = "u2",
            FlashcardSetId = set.Id,
            Mode = StudyMode.Flashcard,
            Score = null,
            CompletedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _observer.OnStudyEventAsync(new StudySessionCompletedEvent(
            "u2",
            DateTime.UtcNow,
            SetId: set.Id,
            SessionId: 10,
            Mode: StudyMode.Flashcard,
            Score: null));

        var unlocked = await _context.UserAchievements.SingleAsync(a => a.UserId == "u2");
        Assert.Equal(AchievementCatalog.FirstFlashcardSession, unlocked.Code);
        Assert.Equal("Buổi Flashcard đầu tiên", unlocked.Title);
    }

    // Dictation điểm 100 → mở cả buổi Dictation đầu và điểm tuyệt đối
    [Fact]
    public async Task Perfect_dictation_unlocks_session_and_perfect_badges()
    {
        // Snapshot cần có session Dictation Score=100 trong DB
        var set = await SeedSetAsync("u3", "Dictation set");
        _context.StudySessions.Add(new StudySession
        {
            UserId = "u3",
            FlashcardSetId = set.Id,
            Mode = StudyMode.Dictation,
            Score = 100,
            CompletedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        await _observer.OnStudyEventAsync(new StudySessionCompletedEvent(
            "u3",
            DateTime.UtcNow,
            SetId: set.Id,
            SessionId: 2,
            Mode: StudyMode.Dictation,
            Score: 100));

        var codes = await _context.UserAchievements
            .Where(a => a.UserId == "u3")
            .Select(a => a.Code)
            .ToListAsync();

        Assert.Contains(AchievementCatalog.FirstDictationSession, codes);
        Assert.Contains(AchievementCatalog.DictationPerfectSession, codes);
    }
}
