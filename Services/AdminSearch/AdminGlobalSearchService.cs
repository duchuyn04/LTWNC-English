using System.Text.RegularExpressions;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.AdminSearch;

public sealed partial class AdminGlobalSearchService : IAdminGlobalSearchService
{
    public const int DefaultPerTypeLimit = 5;
    public const int MaxPerTypeLimit = 10;
    public const int MaxQueryLength = 100;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    // Nhận DbContext và đồng hồ để truy vấn read-only và suy ra trạng thái tài khoản ổn định trong test.
    public AdminGlobalSearchService(
        AppDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    // Tìm kiếm theo từng loại đối tượng, chỉ trả metadata nhận diện an toàn và link Admin tương ứng.
    public async Task<AdminGlobalSearchResult> SearchAsync(
        AdminGlobalSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        NormalizedSearchTerm term = NormalizeQuery(query.Query);
        int perTypeLimit = Math.Clamp(query.PerTypeLimit, 1, MaxPerTypeLimit);
        if (string.IsNullOrWhiteSpace(term.Value))
        {
            return BuildEmptyResult(term);
        }

        AdminGlobalSearchGroup users =
            await SearchUsersAsync(term.Value, perTypeLimit, cancellationToken);
        AdminGlobalSearchGroup sets =
            await SearchFlashcardSetsAsync(term.Value, perTypeLimit, cancellationToken);
        AdminGlobalSearchGroup missions =
            await SearchEnglishMissionsAsync(term.Value, perTypeLimit, cancellationToken);

        return new AdminGlobalSearchResult(
            term.Original,
            term.Value,
            term.WasTruncated,
            new[] { users, sets, missions });
    }

    // Trả kết quả rỗng nhưng vẫn giữ đủ nhóm để view hiển thị nhất quán.
    private static AdminGlobalSearchResult BuildEmptyResult(NormalizedSearchTerm term)
    {
        return new AdminGlobalSearchResult(
            term.Original,
            term.Value,
            term.WasTruncated,
            new[]
            {
                BuildGroup("user", "Người dùng", "/Admin/Users", false, Array.Empty<AdminGlobalSearchItem>()),
                BuildGroup("flashcard-set", "Bộ flashcard", "/Admin/Content", false, Array.Empty<AdminGlobalSearchItem>()),
                BuildGroup("english-mission", "Nhiệm vụ tiếng Anh", "/Admin/EnglishMissions", false, Array.Empty<AdminGlobalSearchItem>())
            });
    }

    // Tìm user bằng email/tên đăng nhập chuẩn hóa hoặc prefix mã định danh; không đọc dữ liệu học tập.
    private async Task<AdminGlobalSearchGroup> SearchUsersAsync(
        string term,
        int perTypeLimit,
        CancellationToken cancellationToken)
    {
        string normalizedToken = term.ToUpperInvariant();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<UserSearchRow> users = await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.Id.StartsWith(term)
                || (user.NormalizedEmail != null && user.NormalizedEmail.StartsWith(normalizedToken))
                || (user.NormalizedUserName != null && user.NormalizedUserName.StartsWith(normalizedToken)))
            .OrderBy(user => user.Email)
            .ThenBy(user => user.UserName)
            .Take(perTypeLimit + 1)
            .Select(user => new UserSearchRow(
                user.Id,
                user.UserName,
                user.Email,
                user.LockoutEnd))
            .ToListAsync(cancellationToken);

        // Suy ra trạng thái sau khi SQL trả về để mapping dễ đọc và không cần toán tử 3 ngôi.
        List<AdminGlobalSearchItem> rows = users
            .Select(user => ToUserSearchItem(user, now))
            .ToList();

        bool hasMore = TrimToLimit(rows, perTypeLimit);
        string seeMoreUrl = "/Admin/Users?search=" + Uri.EscapeDataString(term);
        return BuildGroup("user", "Người dùng", seeMoreUrl, hasMore, rows);
    }

    // Dựng kết quả user an toàn, chỉ gồm định danh, email và trạng thái khóa/mở.
    private static AdminGlobalSearchItem ToUserSearchItem(
        UserSearchRow user,
        DateTimeOffset now)
    {
        string primaryText = user.Id;
        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            primaryText = user.UserName;
        }

        string secondaryText = user.Id;
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            secondaryText = user.Email;
        }

        string status = "Đang mở";
        if (user.LockoutEnd != null && user.LockoutEnd > now)
        {
            status = "Đang khóa";
        }

        return new AdminGlobalSearchItem(
            "user",
            primaryText,
            secondaryText,
            status,
            "/Admin/Users/" + Uri.EscapeDataString(user.Id));
    }

    // Tìm bộ flashcard bằng mã SET-{id}, #id hoặc prefix tiêu đề; không đọc mặt trước/mặt sau của thẻ.
    private async Task<AdminGlobalSearchGroup> SearchFlashcardSetsAsync(
        string term,
        int perTypeLimit,
        CancellationToken cancellationToken)
    {
        int? setId = TryParseCode(term, "SET");
        List<AdminGlobalSearchItem> rows = await _context.FlashcardSets
            .AsNoTracking()
            .Where(set =>
                (setId != null && set.Id == setId.Value)
                || set.Title.StartsWith(term))
            .OrderBy(set => set.Title)
            .ThenBy(set => set.Id)
            .Take(perTypeLimit + 1)
            .Select(set => new AdminGlobalSearchItem(
                "flashcard-set",
                set.Title,
                "SET-" + set.Id,
                BuildSetStatus(set.IsPublic, set.ModerationStatus),
                "/Admin/Content/" + set.Id))
            .ToListAsync(cancellationToken);

        bool hasMore = TrimToLimit(rows, perTypeLimit);
        string seeMoreUrl = "/Admin/Content?search=" + Uri.EscapeDataString(term);
        return BuildGroup("flashcard-set", "Bộ flashcard", seeMoreUrl, hasMore, rows);
    }

    // Tìm nhiệm vụ tiếng Anh bằng mã EM-{id}; không tìm trong hội thoại, đáp án, target words hoặc prompt nội bộ.
    private async Task<AdminGlobalSearchGroup> SearchEnglishMissionsAsync(
        string term,
        int perTypeLimit,
        CancellationToken cancellationToken)
    {
        int? missionId = TryParseRequiredPrefixedCode(term, "EM");
        if (missionId == null)
        {
            return BuildGroup(
                "english-mission",
                "Nhiệm vụ tiếng Anh",
                "/Admin/EnglishMissions?search=" + Uri.EscapeDataString(term),
                false,
                Array.Empty<AdminGlobalSearchItem>());
        }

        List<AdminGlobalSearchItem> rows = await _context.EnglishMissions
            .AsNoTracking()
            .Where(mission => mission.Id == missionId.Value)
            .OrderByDescending(mission => mission.CreatedAt)
            .Take(perTypeLimit + 1)
            .Select(mission => new AdminGlobalSearchItem(
                "english-mission",
                "EM-" + mission.Id,
                mission.Title,
                mission.Status,
                "/Admin/EnglishMissions/" + mission.Id))
            .ToListAsync(cancellationToken);

        bool hasMore = TrimToLimit(rows, perTypeLimit);
        string seeMoreUrl = "/Admin/EnglishMissions?search=" + Uri.EscapeDataString(term);
        return BuildGroup("english-mission", "Nhiệm vụ tiếng Anh", seeMoreUrl, hasMore, rows);
    }

    // Gom dữ liệu nhóm để controller/view không phải biết chi tiết từng loại truy vấn.
    private static AdminGlobalSearchGroup BuildGroup(
        string type,
        string label,
        string seeMoreUrl,
        bool hasMore,
        IReadOnlyList<AdminGlobalSearchItem> items)
    {
        return new AdminGlobalSearchGroup(type, label, seeMoreUrl, hasMore, items);
    }

    // Ghép trạng thái bộ thành một nhãn ngắn, không kèm lý do kiểm duyệt hay nội dung riêng tư.
    private static string BuildSetStatus(bool isPublic, string moderationStatus)
    {
        string visibility = "Riêng tư";
        if (isPublic)
        {
            visibility = "Công khai";
        }

        return visibility + " · " + moderationStatus;
    }

    // Cắt phần tử dư được lấy bằng limit + 1 để biết có còn kết quả cho link xem thêm.
    private static bool TrimToLimit(List<AdminGlobalSearchItem> rows, int perTypeLimit)
    {
        if (rows.Count <= perTypeLimit)
        {
            return false;
        }

        rows.RemoveAt(rows.Count - 1);
        return true;
    }

    // Chuẩn hóa query: trim, gom khoảng trắng, bỏ ký tự điều khiển và giới hạn độ dài trước khi vào SQL.
    private static NormalizedSearchTerm NormalizeQuery(string? query)
    {
        string original = string.Empty;
        if (query != null)
        {
            original = query;
        }

        string withoutControlChars = ControlCharacters().Replace(original, " ");
        string compacted = Whitespace().Replace(withoutControlChars.Trim(), " ");
        bool wasTruncated = compacted.Length > MaxQueryLength;
        if (wasTruncated)
        {
            compacted = compacted[..MaxQueryLength];
        }

        return new NormalizedSearchTerm(original, compacted, wasTruncated);
    }

    // Nhận các dạng mã an toàn như SET-12, #12 hoặc 12 cho các loại không cần gate nhạy cảm riêng.
    private static int? TryParseCode(string term, string prefix)
    {
        string normalized = term.Trim();
        if (normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[(prefix.Length + 1)..];
        }
        else if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prefix.Length..];
        }
        else if (normalized.StartsWith("#", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (int.TryParse(normalized, out int id) && id > 0)
        {
            return id;
        }

        return null;
    }

    // Nhận mã bắt buộc có prefix, dùng cho dữ liệu nhạy cảm để chuỗi số chung không lộ sự tồn tại của mission.
    private static int? TryParseRequiredPrefixedCode(string term, string prefix)
    {
        string normalized = term.Trim();
        if (normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[(prefix.Length + 1)..];
        }
        else if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prefix.Length..];
        }
        else
        {
            return null;
        }

        if (int.TryParse(normalized, out int id) && id > 0)
        {
            return id;
        }

        return null;
    }

    // Regex gom nhiều khoảng trắng thành một khoảng trắng trước khi truy vấn.
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // Regex loại bỏ ký tự điều khiển để query không mang dữ liệu khó hiển thị/log.
    [GeneratedRegex(@"[\u0000-\u001F\u007F]")]
    private static partial Regex ControlCharacters();

    private sealed record NormalizedSearchTerm(
        string Original,
        string Value,
        bool WasTruncated);

    private sealed record UserSearchRow(
        string Id,
        string? UserName,
        string? Email,
        DateTimeOffset? LockoutEnd);
}
