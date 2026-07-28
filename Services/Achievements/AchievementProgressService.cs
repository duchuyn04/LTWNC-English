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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
    }

    // Đếm toàn bộ metric cho một user qua batch seam dùng chung.
    public async Task<AchievementProgressSnapshot> GetSnapshotAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `GetSnapshotsAsync` và lưu kết quả vào `snapshots`.
        IReadOnlyDictionary<string, AchievementProgressSnapshot> snapshots =
            await GetSnapshotsAsync([userId], cancellationToken);
        // 2. Trả `snapshots[userId]` cho nơi gọi.
        return snapshots[userId];
    }

    // Gom metric của nhiều user bằng ba truy vấn tổng hợp, không lặp truy vấn theo từng dòng giao diện.
    public async Task<IReadOnlyDictionary<string, AchievementProgressSnapshot>> GetSnapshotsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ToArray` và lưu kết quả vào `requestedUserIds`.
        string[] requestedUserIds = userIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        // 2. Kiểm tra `requestedUserIds.Length == 0` để chọn nhánh xử lý phù hợp.
        if (requestedUserIds.Length == 0)
        {
            // 3. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new Dictionary<string, AchievementProgressSnapshot>(StringComparer.Ordinal);
        }

        // 4. Gọi `ToListAsync` và lưu kết quả vào `masteredCardCounts`.
        List<UserCountAggregate> masteredCardCounts = await _context.UserProgresses
            .AsNoTracking()
            .Where(progress => requestedUserIds.Contains(progress.UserId) && progress.IsLearned)
            .GroupBy(progress => progress.UserId)
            .Select(group => new UserCountAggregate(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        // 5. Gọi `ToDictionary` và lưu kết quả vào `masteredCardsByUser`.
        Dictionary<string, int> masteredCardsByUser = masteredCardCounts
            .ToDictionary(item => item.UserId, item => item.Count, StringComparer.Ordinal);

        // 6. Gọi `ToListAsync` và lưu kết quả vào `sessionMetrics`.
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
        // 7. Gọi `ToDictionary` và lưu kết quả vào `sessionMetricsByUser`.
        Dictionary<string, UserSessionMetricAggregate> sessionMetricsByUser = sessionMetrics
            .ToDictionary(metric => metric.UserId, StringComparer.Ordinal);

        // 8. Gọi `ToDictionaryAsync` và lưu kết quả vào `correctAnswersByUser`.
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
        // 9. Khởi tạo `snapshots` với dữ liệu ban đầu cần thiết.
        var snapshots = new Dictionary<string, AchievementProgressSnapshot>(StringComparer.Ordinal);
        // 10. Duyệt từng `userId` trong `requestedUserIds` để xử lý lần lượt.
        foreach (string userId in requestedUserIds)
        {
            // 11. Gọi `TryGetValue` để thực hiện bước nghiệp vụ này.
            masteredCardsByUser.TryGetValue(userId, out int cardsMastered);
            // 12. Gọi `TryGetValue` để thực hiện bước nghiệp vụ này.
            correctAnswersByUser.TryGetValue(userId, out int correctAnswers);
            // 13. Gọi `TryGetValue` để thực hiện bước nghiệp vụ này.
            sessionMetricsByUser.TryGetValue(userId, out UserSessionMetricAggregate? sessions);

            // 14. Tính giá trị và lưu vào `flashcardSessions` để dùng ở bước tiếp theo.
            int flashcardSessions = 0;
            // 15. Tính giá trị và lưu vào `dictationSessions` để dùng ở bước tiếp theo.
            int dictationSessions = 0;
            // 16. Tính giá trị và lưu vào `dictationPerfectSessions` để dùng ở bước tiếp theo.
            int dictationPerfectSessions = 0;
            // 17. Kiểm tra `sessions != null` để chọn nhánh xử lý phù hợp.
            if (sessions != null)
            {
                // 18. Cập nhật `flashcardSessions` bằng giá trị mới.
                flashcardSessions = sessions.FlashcardSessions;
                // 19. Cập nhật `dictationSessions` bằng giá trị mới.
                dictationSessions = sessions.DictationSessions;
                // 20. Cập nhật `dictationPerfectSessions` bằng giá trị mới.
                dictationPerfectSessions = sessions.DictationPerfectSessions;
            }

            // 21. Cập nhật `snapshots[userId]` bằng giá trị mới.
            snapshots[userId] = new AchievementProgressSnapshot
            {
                CardsMastered = cardsMastered,
                FlashcardSessions = flashcardSessions,
                DictationSessions = dictationSessions,
                DictationCorrectAnswers = correctAnswers,
                DictationPerfectSessions = dictationPerfectSessions
            };
        }

        // 22. Trả `snapshots` cho nơi gọi.
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
