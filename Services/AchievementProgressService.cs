using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services;

// Đếm metric thành tích của một user (thẻ thuộc, buổi học, nghe chép...).
// Kết quả dùng cho progress bar và để quyết định mở khóa huy hiệu.
public class AchievementProgressService : IAchievementProgressService
{
    // DbContext EF Core, query bảng progress / session / dictation
    private readonly AppDbContext _context;

    // Inject DbContext
    public AchievementProgressService(AppDbContext context)
    {
        _context = context;
    }

    // Đếm một lần toàn bộ metric rồi gói vào snapshot (tránh query lặp theo từng huy hiệu)
    public async Task<AchievementProgressSnapshot> GetSnapshotAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Thẻ user đã đánh dấu thuộc
        int cardsMastered = await _context.UserProgresses
            .CountAsync(
                progress => progress.UserId == userId && progress.IsLearned,
                cancellationToken);

        // Buổi Flashcard đã hoàn thành
        int flashcardSessions = await _context.StudySessions
            .CountAsync(
                session => session.UserId == userId && session.Mode == StudyMode.Flashcard,
                cancellationToken);

        // Buổi Dictation (mọi điểm)
        int dictationSessions = await _context.StudySessions
            .CountAsync(
                session => session.UserId == userId && session.Mode == StudyMode.Dictation,
                cancellationToken);

        // Buổi Dictation điểm 100
        int dictationPerfectSessions = await _context.StudySessions
            .CountAsync(
                session =>
                    session.UserId == userId
                    && session.Mode == StudyMode.Dictation
                    && session.Score == 100,
                cancellationToken);

        // Câu nghe chép đúng: join detail với session để lọc đúng user
        int dictationCorrectAnswers = await (
            from detail in _context.DictationSessionDetails
            join session in _context.StudySessions on detail.StudySessionId equals session.Id
            where session.UserId == userId && detail.IsCorrect
            select detail
        ).CountAsync(cancellationToken);

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
