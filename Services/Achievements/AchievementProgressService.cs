using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Achievements;

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

    // Đếm toàn bộ metric cho một user qua batch seam dùng chung.
    public async Task<AchievementProgressSnapshot> GetSnapshotAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, AchievementProgressSnapshot> snapshots =
            await GetSnapshotsAsync([userId], cancellationToken);
        return snapshots[userId];
    }

    // Gom metric của nhiều user bằng ba truy vấn tổng hợp, không lặp truy vấn theo từng dòng giao diện.
    public async Task<IReadOnlyDictionary<string, AchievementProgressSnapshot>> GetSnapshotsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        string[] requestedUserIds = userIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requestedUserIds.Length == 0)
        {
            return new Dictionary<string, AchievementProgressSnapshot>(StringComparer.Ordinal);
        }

        List<UserCountAggregate> masteredCardCounts = await _context.UserProgresses
            .AsNoTracking()
            .Where(progress => requestedUserIds.Contains(progress.UserId) && progress.IsLearned)
            .GroupBy(progress => progress.UserId)
            .Select(group => new UserCountAggregate(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        Dictionary<string, int> masteredCardsByUser = masteredCardCounts
            .ToDictionary(item => item.UserId, item => item.Count, StringComparer.Ordinal);

        List<UserSessionMetricAggregate> sessionMetrics = await _context.StudySessions
            .AsNoTracking()
            .Where(session => requestedUserIds.Contains(session.UserId)
                && session.CompletedAt.HasValue)
            .GroupBy(session => session.UserId)
            .Select(group => new UserSessionMetricAggregate(
                group.Key,
                group.Count(session => session.Mode == StudyMode.Flashcard),
                group.Count(session => session.Mode == StudyMode.Dictation),
                group.Count(session => session.Mode == StudyMode.Dictation && session.Score == 100)))
            .ToListAsync(cancellationToken);
        Dictionary<string, UserSessionMetricAggregate> sessionMetricsByUser = sessionMetrics
            .ToDictionary(metric => metric.UserId, StringComparer.Ordinal);

        Dictionary<string, int> correctAnswersByUser = await (
            from detail in _context.DictationSessionDetails.AsNoTracking()
            join session in _context.StudySessions.AsNoTracking()
                on detail.StudySessionId equals session.Id
            where requestedUserIds.Contains(session.UserId)
                && session.CompletedAt.HasValue
                && detail.IsCorrect
            group detail by session.UserId into answers
            select new UserCorrectAnswerAggregate(answers.Key, answers.Count())
        ).ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);

        // Tạo cả snapshot rỗng cho user chưa có hoạt động để caller không phải xử lý trường hợp thiếu khóa.
        var snapshots = new Dictionary<string, AchievementProgressSnapshot>(StringComparer.Ordinal);
        foreach (string userId in requestedUserIds)
        {
            masteredCardsByUser.TryGetValue(userId, out int cardsMastered);
            correctAnswersByUser.TryGetValue(userId, out int correctAnswers);
            sessionMetricsByUser.TryGetValue(userId, out UserSessionMetricAggregate? sessions);

            int flashcardSessions = 0;
            int dictationSessions = 0;
            int dictationPerfectSessions = 0;
            if (sessions != null)
            {
                flashcardSessions = sessions.FlashcardSessions;
                dictationSessions = sessions.DictationSessions;
                dictationPerfectSessions = sessions.DictationPerfectSessions;
            }

            snapshots[userId] = new AchievementProgressSnapshot
            {
                CardsMastered = cardsMastered,
                FlashcardSessions = flashcardSessions,
                DictationSessions = dictationSessions,
                DictationCorrectAnswers = correctAnswers,
                DictationPerfectSessions = dictationPerfectSessions
            };
        }

        return snapshots;
    }

    private sealed record UserSessionMetricAggregate(
        string UserId,
        int FlashcardSessions,
        int DictationSessions,
        int DictationPerfectSessions);

    private sealed record UserCorrectAnswerAggregate(string UserId, int Count);

    private sealed record UserCountAggregate(string UserId, int Count);
}
