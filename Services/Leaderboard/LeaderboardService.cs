using ltwnc.Data;
using ltwnc.Models.ViewModels.Leaderboard;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Leaderboard;

public sealed class LeaderboardService : ILeaderboardService
{
    private const int TopEntryLimit = 20;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public LeaderboardService(AppDbContext db, TimeProvider? timeProvider = null)
    {
        // 1. Lưu dependency `_db` để các phương thức khác sử dụng.
        _db = db;
        // 2. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LeaderboardPageViewModel> GetPageAsync(
        int periodDays,
        string? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Tính giá trị và lưu vào `normalizedPeriod` để dùng ở bước tiếp theo.
        int normalizedPeriod = periodDays == 30 ? 30 : 7;
        // 2. Gọi `AddDays` và lưu kết quả vào `cutoff`.
        DateTime cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-normalizedPeriod);

        // 3. Gọi `ToListAsync` và lưu kết quả vào `grouped`.
        var grouped = await (
            from session in _db.StudySessions.AsNoTracking()
            join profile in _db.UserProfiles.AsNoTracking()
                on session.UserId equals profile.UserId
            join user in _db.AppUsers.AsNoTracking()
                on session.UserId equals user.Id
            where profile.IsPublic
                && profile.ShowStats
                && session.CompletedAt.HasValue
                && session.CompletedAt.Value >= cutoff
                && session.DurationSeconds.HasValue
                && session.DurationSeconds.Value > 0
            select new
            {
                session.UserId,
                Username = user.UserName ?? string.Empty,
                profile.AvatarPath,
                session.DurationSeconds
            })
            .GroupBy(row => new { row.UserId, row.Username, row.AvatarPath })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.Username,
                group.Key.AvatarPath,
                TotalSeconds = group.Sum(row => (long)row.DurationSeconds!.Value),
                SessionCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        // 4. Gọi `ToList` và lưu kết quả vào `ranked`.
        List<LeaderboardEntryViewModel> ranked = grouped
            .OrderByDescending(row => row.TotalSeconds)
            .ThenByDescending(row => row.SessionCount)
            .ThenBy(row => row.Username)
            .ThenBy(row => row.UserId)
            .Select((row, index) => new LeaderboardEntryViewModel
            {
                Rank = index + 1,
                UserId = row.UserId,
                Username = row.Username,
                AvatarPath = row.AvatarPath,
                AvatarInitial = AvatarInitial(row.Username),
                TotalSeconds = row.TotalSeconds,
                SessionCount = row.SessionCount,
                IsViewer = !string.IsNullOrWhiteSpace(viewerUserId)
                    && row.UserId == viewerUserId
            })
            .ToList();

        // 5. Gọi `FirstOrDefault` và lưu kết quả vào `viewerEntry`.
        LeaderboardEntryViewModel? viewerEntry = ranked
            .FirstOrDefault(entry => entry.IsViewer);

        // 6. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new LeaderboardPageViewModel
        {
            PeriodDays = normalizedPeriod,
            Entries = ranked.Take(TopEntryLimit).ToList(),
            ViewerEntry = viewerEntry
        };
    }

    private static string AvatarInitial(string username)
    {
        // 1. Trả `string.IsNullOrWhiteSpace(username) ? "?" : username.Trim()[0].ToSt...` cho nơi gọi.
        return string.IsNullOrWhiteSpace(username)
            ? "?"
            : username.Trim()[0].ToString().ToUpperInvariant();
    }
}
