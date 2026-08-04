using ltwnc.Data;
using ltwnc.Models.ViewModels.Leaderboard;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Leaderboard;

public sealed class LeaderboardService : ILeaderboardService
{
    // Chỉ hiển thị tối đa 20 người đầu bảng
    private const int TopEntryLimit = 20;

    // Dùng để truy vấn dữ liệu từ database
    private readonly AppDbContext _db;

    // Cung cấp thời gian hiện tại
    private readonly TimeProvider _timeProvider;

    // Nếu không truyền TimeProvider thì sử dụng thời gian hệ thống
    public LeaderboardService(AppDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LeaderboardPageViewModel> GetPageAsync(
        int periodDays,
        string? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        // Chấp nhận 7 ngày, 30 ngày hoặc 0 cho toàn bộ thời gian.
        int normalizedPeriod = periodDays is 0 or 30 ? periodDays : 7;

        // Bảng toàn thời gian không cần mốc bắt đầu.
        DateTime? cutoff = normalizedPeriod > 0
            ? _timeProvider.GetUtcNow().UtcDateTime.AddDays(-normalizedPeriod)
            : null;

        // Lấy và tổng hợp dữ liệu học tập theo từng người dùng
        var grouped = await (
            from session in _db.StudySessions.AsNoTracking()

            // Ghép phiên học với hồ sơ người dùng
            join profile in _db.UserProfiles.AsNoTracking()
                on session.UserId equals profile.UserId

            // Ghép thêm tài khoản để lấy username
            join user in _db.AppUsers.AsNoTracking()
                on session.UserId equals user.Id
            // Chỉ lấy những phiên học đủ điều kiện xuất hiện trên bảng xếp hạng
            where profile.IsPublic
                && session.CompletedAt.HasValue
                && (!cutoff.HasValue || session.CompletedAt.Value >= cutoff.Value)
                && session.DurationSeconds.HasValue
                && session.DurationSeconds.Value > 0

            // Chỉ lấy các trường cần thiết
            select new
            {
                session.UserId,
                Username = user.UserName ?? string.Empty,
                profile.AvatarPath,
                session.DurationSeconds
            })
            // Gom tất cả phiên học thuộc cùng một người dùng
            .GroupBy(row => new { row.UserId, row.Username, row.AvatarPath })

            // Tính tổng thời gian học và số phiên học của mỗi người
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.Username,
                group.Key.AvatarPath,
                TotalSeconds = group.Sum(row => (long)row.DurationSeconds!.Value),
                SessionCount = group.Count()
            })
            // Thực thi truy vấn và tải kết quả về bộ nhớ
            .ToListAsync(cancellationToken);
        // Sắp xếp người dùng và gán thứ hạng
        List<LeaderboardEntryViewModel> ranked = grouped
            // Ưu tiên người có tổng thời gian học cao hơn
            .OrderByDescending(row => row.TotalSeconds)
            // Nếu bằng thời gian, ưu tiên người có nhiều phiên hơn
            .ThenByDescending(row => row.SessionCount)
            // Nếu vẫn bằng nhau, sắp xếp theo username
            .ThenBy(row => row.Username)
            // Dùng UserId làm tiêu chí cuối để thứ tự ổn định
            .ThenBy(row => row.UserId)

            // Chuyển mỗi kết quả thành một dòng bảng xếp hạng.
            // index bắt đầu từ 0 nên Rank phải cộng thêm 1.
            .Select((row, index) => new LeaderboardEntryViewModel
            {
                Rank = index + 1,
                UserId = row.UserId,
                Username = row.Username,
                AvatarPath = row.AvatarPath,
                AvatarInitial = AvatarInitial(row.Username), // Tạo chữ cái đại diện nếu không có ảnh avatar
                TotalSeconds = row.TotalSeconds,
                SessionCount = row.SessionCount,
                // Đánh dấu đây có phải người đang xem hay không
                IsViewer = !string.IsNullOrWhiteSpace(viewerUserId)
                    && row.UserId == viewerUserId
            })
            .ToList();
        /*
        Tìm dòng xếp hạng của người đang xem.
        Có thể không tìm thấy nếu chưa đăng nhập
        hoặc không có phiên học hợp lệ.
         */


        LeaderboardEntryViewModel? viewerEntry = ranked
            .FirstOrDefault(entry => entry.IsViewer);

        // Trả dữ liệu đã chuẩn bị cho Controller và View.
        return new LeaderboardPageViewModel
        {
            PeriodDays = normalizedPeriod,
            TotalEntryCount = ranked.Count,
            Entries = ranked.Take(TopEntryLimit).ToList(), // Chỉ hiển thị 20 người đứng đầu
            // Vẫn trả vị trí người đang xem,
            // kể cả khi họ không nằm trong top 20
            ViewerEntry = viewerEntry
        };
    }

    // Lấy chữ cái đầu của username để làm avatar mặc định
    private static string AvatarInitial(string username)
    {
        return string.IsNullOrWhiteSpace(username)
            ? "?"
            : username.Trim()[0].ToString().ToUpperInvariant();
    }
}
