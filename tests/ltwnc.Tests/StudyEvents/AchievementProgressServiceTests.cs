using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Tests.StudyEvents;

// Kiểm tra service đọc snapshot tiến độ metric theo từng user
public class AchievementProgressServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AchievementProgressService _sut;

    public AchievementProgressServiceTests()
    {
        // Database giả trong bộ nhớ, mỗi test một tên riêng
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new AchievementProgressService(_context);
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

    // Tạo bộ thẻ dùng cho StudySession / Dictation detail
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

    // Tạo thẻ thuộc bộ đã có
    private async Task<Flashcard> SeedCardAsync(int setId, string front)
    {
        var card = new Flashcard
        {
            FlashcardSetId = setId,
            FrontText = front,
            BackText = "back",
            Pronunciation = "/a/",
            PartOfSpeech = "noun",
            ExampleSentence = "ex",
            ExampleMeaning = "nghĩa",
            OrderIndex = 0
        };
        _context.Flashcards.Add(card);
        await _context.SaveChangesAsync();
        return card;
    }

    // Seed đầy đủ metric cho u1: 3 thẻ thuộc, 2 flashcard, 1 dictation 100, 4 câu đúng
    private async Task SeedU1MetricsAsync()
    {
        await SeedMasteredCardsAsync("u1", 3);

        var set = await SeedSetAsync("u1", "Study set u1");
        var card = await SeedCardAsync(set.Id, "dict-front");

        // 2 buổi Flashcard
        for (var i = 0; i < 2; i++)
        {
            _context.StudySessions.Add(new StudySession
            {
                UserId = "u1",
                FlashcardSetId = set.Id,
                Mode = StudyMode.Flashcard,
                Score = null,
                CompletedAt = DateTime.UtcNow
            });
        }

        // 1 buổi Dictation điểm tuyệt đối
        var dictation = new StudySession
        {
            UserId = "u1",
            FlashcardSetId = set.Id,
            Mode = StudyMode.Dictation,
            Score = 100,
            CompletedAt = DateTime.UtcNow
        };
        _context.StudySessions.Add(dictation);
        await _context.SaveChangesAsync();

        // 4 câu nghe chép đúng (có thể kèm 1 câu sai để không bị đếm nhầm)
        for (var i = 0; i < 4; i++)
        {
            _context.DictationSessionDetails.Add(new DictationSessionDetail
            {
                StudySessionId = dictation.Id,
                FlashcardId = card.Id,
                IsCorrect = true,
                AnsweredText = $"ok-{i}"
            });
        }

        _context.DictationSessionDetails.Add(new DictationSessionDetail
        {
            StudySessionId = dictation.Id,
            FlashcardId = card.Id,
            IsCorrect = false,
            AnsweredText = "wrong"
        });

        await _context.SaveChangesAsync();
    }

    // Seed dữ liệu u2 để kiểm tra không lẫn user
    private async Task SeedU2NoiseAsync()
    {
        await SeedMasteredCardsAsync("u2", 10);

        var set = await SeedSetAsync("u2", "Study set u2");
        var card = await SeedCardAsync(set.Id, "u2-front");

        for (var i = 0; i < 5; i++)
        {
            _context.StudySessions.Add(new StudySession
            {
                UserId = "u2",
                FlashcardSetId = set.Id,
                Mode = StudyMode.Flashcard,
                CompletedAt = DateTime.UtcNow
            });
        }

        var dictation = new StudySession
        {
            UserId = "u2",
            FlashcardSetId = set.Id,
            Mode = StudyMode.Dictation,
            Score = 100,
            CompletedAt = DateTime.UtcNow
        };
        _context.StudySessions.Add(dictation);
        await _context.SaveChangesAsync();

        for (var i = 0; i < 8; i++)
        {
            _context.DictationSessionDetails.Add(new DictationSessionDetail
            {
                StudySessionId = dictation.Id,
                FlashcardId = card.Id,
                IsCorrect = true,
                AnsweredText = $"u2-{i}"
            });
        }

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSnapshotAsync_counts_all_metrics_for_user()
    {
        await SeedU1MetricsAsync();

        var snapshot = await _sut.GetSnapshotAsync("u1");

        Assert.Equal(3, snapshot.CardsMastered);
        Assert.Equal(2, snapshot.FlashcardSessions);
        Assert.Equal(1, snapshot.DictationSessions);
        Assert.Equal(4, snapshot.DictationCorrectAnswers);
        Assert.Equal(1, snapshot.DictationPerfectSessions);

        // GetValue map đúng từng metric kind
        Assert.Equal(3, snapshot.GetValue(AchievementMetricKind.CardsMastered));
        Assert.Equal(2, snapshot.GetValue(AchievementMetricKind.FlashcardSessions));
        Assert.Equal(1, snapshot.GetValue(AchievementMetricKind.DictationSessions));
        Assert.Equal(4, snapshot.GetValue(AchievementMetricKind.DictationCorrectAnswers));
        Assert.Equal(1, snapshot.GetValue(AchievementMetricKind.DictationPerfectSessions));
    }

    [Fact]
    public async Task GetSnapshotAsync_does_not_count_other_users()
    {
        await SeedU1MetricsAsync();
        await SeedU2NoiseAsync();

        var snapshot = await _sut.GetSnapshotAsync("u1");

        Assert.Equal(3, snapshot.CardsMastered);
        Assert.Equal(2, snapshot.FlashcardSessions);
        Assert.Equal(1, snapshot.DictationSessions);
        Assert.Equal(4, snapshot.DictationCorrectAnswers);
        Assert.Equal(1, snapshot.DictationPerfectSessions);
    }
}
