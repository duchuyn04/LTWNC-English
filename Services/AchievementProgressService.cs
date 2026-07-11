using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services;

// ============================================================
// Service đếm các chỉ số thành tích (thẻ thuộc, buổi học, nghe chép…)
// cho một user — trả về AchievementProgressSnapshot dùng cho progress bar
// và để AchievementUnlockService quyết định mở khóa huy hiệu.
// ============================================================
public class AchievementProgressService
{
    private readonly AppDbContext _context;

    public AchievementProgressService(AppDbContext context)
    {
        _context = context;
    }

    // Lấy snapshot đầy đủ các metric của user (đếm một lần, tái sử dụng)
    public async Task<AchievementProgressSnapshot> GetSnapshotAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Đếm thẻ đã thuộc
        var cardsMastered = await _context.UserProgresses
            .CountAsync(p => p.UserId == userId && p.IsLearned, cancellationToken);

        // Đếm buổi Flashcard
        var flashcardSessions = await _context.StudySessions
            .CountAsync(
                s => s.UserId == userId && s.Mode == StudyMode.Flashcard,
                cancellationToken);

        // Đếm buổi Dictation (mọi điểm số)
        var dictationSessions = await _context.StudySessions
            .CountAsync(
                s => s.UserId == userId && s.Mode == StudyMode.Dictation,
                cancellationToken);

        // Đếm buổi Dictation đạt 100 điểm
        var dictationPerfectSessions = await _context.StudySessions
            .CountAsync(
                s => s.UserId == userId && s.Mode == StudyMode.Dictation && s.Score == 100,
                cancellationToken);

        // Đếm câu nghe chép đúng — join session để lọc đúng UserId
        var dictationCorrectAnswers = await (
            from d in _context.DictationSessionDetails
            join s in _context.StudySessions on d.StudySessionId equals s.Id
            where s.UserId == userId && d.IsCorrect
            select d).CountAsync(cancellationToken);

        return new AchievementProgressSnapshot
        {
            CardsMastered = cardsMastered,
            FlashcardSessions = flashcardSessions,
            DictationSessions = dictationSessions,
            DictationCorrectAnswers = dictationCorrectAnswers,
            DictationPerfectSessions = dictationPerfectSessions
        };
    }
}
