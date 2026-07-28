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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    // Tìm kiếm theo từng loại đối tượng, chỉ trả metadata nhận diện an toàn và link Admin tương ứng.
    public async Task<AdminGlobalSearchResult> SearchAsync(
        AdminGlobalSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `NormalizeQuery` và lưu kết quả vào `term`.
        NormalizedSearchTerm term = NormalizeQuery(query.Query);
        // 2. Gọi `Clamp` và lưu kết quả vào `perTypeLimit`.
        int perTypeLimit = Math.Clamp(query.PerTypeLimit, 1, MaxPerTypeLimit);
        // 3. Kiểm tra `string.IsNullOrWhiteSpace(term.Value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(term.Value))
        {
            // 4. Trả kết quả từ `BuildEmptyResult` cho nơi gọi.
            return BuildEmptyResult(term);
        }

        // 5. Gọi `SearchUsersAsync` và lưu kết quả vào `users`.
        AdminGlobalSearchGroup users =
            await SearchUsersAsync(term.Value, perTypeLimit, cancellationToken);
        // 6. Gọi `SearchFlashcardSetsAsync` và lưu kết quả vào `sets`.
        AdminGlobalSearchGroup sets =
            await SearchFlashcardSetsAsync(term.Value, perTypeLimit, cancellationToken);
        // 7. Gọi `SearchEnglishMissionsAsync` và lưu kết quả vào `missions`.
        AdminGlobalSearchGroup missions =
            await SearchEnglishMissionsAsync(term.Value, perTypeLimit, cancellationToken);

        // 8. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminGlobalSearchResult(
            term.Original,
            term.Value,
            term.WasTruncated,
            new[] { users, sets, missions });
    }

    // Trả kết quả rỗng nhưng vẫn giữ đủ nhóm để view hiển thị nhất quán.
    private static AdminGlobalSearchResult BuildEmptyResult(NormalizedSearchTerm term)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Gọi `ToUpperInvariant` và lưu kết quả vào `normalizedToken`.
        string normalizedToken = term.ToUpperInvariant();
        // 2. Gọi `GetUtcNow` và lưu kết quả vào `now`.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        // 3. Gọi `ToListAsync` và lưu kết quả vào `users`.
        List<UserSearchRow> users = await _context.AppUsers
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
        // 4. Gọi `ToList` và lưu kết quả vào `rows`.
        List<AdminGlobalSearchItem> rows = users
            .Select(user => ToUserSearchItem(user, now))
            .ToList();

        // 5. Gọi `TrimToLimit` và lưu kết quả vào `hasMore`.
        bool hasMore = TrimToLimit(rows, perTypeLimit);
        // 6. Tính giá trị và lưu vào `seeMoreUrl` để dùng ở bước tiếp theo.
        string seeMoreUrl = "/Admin/Users?search=" + Uri.EscapeDataString(term);
        // 7. Trả kết quả từ `BuildGroup` cho nơi gọi.
        return BuildGroup("user", "Người dùng", seeMoreUrl, hasMore, rows);
    }

    // Dựng kết quả user an toàn, chỉ gồm định danh, email và trạng thái khóa/mở.
    private static AdminGlobalSearchItem ToUserSearchItem(
        UserSearchRow user,
        DateTimeOffset now)
    {
        // 1. Tính giá trị và lưu vào `primaryText` để dùng ở bước tiếp theo.
        string primaryText = user.Id;
        // 2. Kiểm tra `!string.IsNullOrWhiteSpace(user.UserName)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            // 3. Cập nhật `primaryText` bằng giá trị mới.
            primaryText = user.UserName;
        }

        // 4. Tính giá trị và lưu vào `secondaryText` để dùng ở bước tiếp theo.
        string secondaryText = user.Id;
        // 5. Kiểm tra `!string.IsNullOrWhiteSpace(user.Email)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            // 6. Cập nhật `secondaryText` bằng giá trị mới.
            secondaryText = user.Email;
        }

        // 7. Tính giá trị và lưu vào `status` để dùng ở bước tiếp theo.
        string status = "Đang mở";
        // 8. Kiểm tra `user.LockoutEnd != null && user.LockoutEnd > now` để chọn nhánh xử lý phù hợp.
        if (user.LockoutEnd != null && user.LockoutEnd > now)
        {
            // 9. Cập nhật `status` bằng giá trị mới.
            status = "Đang khóa";
        }

        // 10. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Gọi `TryParseCode` và lưu kết quả vào `setId`.
        int? setId = TryParseCode(term, "SET");
        // 2. Gọi `ToListAsync` và lưu kết quả vào `rows`.
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

        // 3. Gọi `TrimToLimit` và lưu kết quả vào `hasMore`.
        bool hasMore = TrimToLimit(rows, perTypeLimit);
        // 4. Tính giá trị và lưu vào `seeMoreUrl` để dùng ở bước tiếp theo.
        string seeMoreUrl = "/Admin/Content?search=" + Uri.EscapeDataString(term);
        // 5. Trả kết quả từ `BuildGroup` cho nơi gọi.
        return BuildGroup("flashcard-set", "Bộ flashcard", seeMoreUrl, hasMore, rows);
    }

    // Tìm nhiệm vụ tiếng Anh bằng mã EM-{id}; không tìm trong hội thoại, đáp án, target words hoặc prompt nội bộ.
    private async Task<AdminGlobalSearchGroup> SearchEnglishMissionsAsync(
        string term,
        int perTypeLimit,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `TryParseRequiredPrefixedCode` và lưu kết quả vào `missionId`.
        int? missionId = TryParseRequiredPrefixedCode(term, "EM");
        // 2. Kiểm tra `missionId == null` để chọn nhánh xử lý phù hợp.
        if (missionId == null)
        {
            // 3. Trả kết quả từ `BuildGroup` cho nơi gọi.
            return BuildGroup(
                "english-mission",
                "Nhiệm vụ tiếng Anh",
                "/Admin/EnglishMissions?search=" + Uri.EscapeDataString(term),
                false,
                Array.Empty<AdminGlobalSearchItem>());
        }

        // 4. Gọi `ToListAsync` và lưu kết quả vào `rows`.
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

        // 5. Gọi `TrimToLimit` và lưu kết quả vào `hasMore`.
        bool hasMore = TrimToLimit(rows, perTypeLimit);
        // 6. Tính giá trị và lưu vào `seeMoreUrl` để dùng ở bước tiếp theo.
        string seeMoreUrl = "/Admin/EnglishMissions?search=" + Uri.EscapeDataString(term);
        // 7. Trả kết quả từ `BuildGroup` cho nơi gọi.
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
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminGlobalSearchGroup(type, label, seeMoreUrl, hasMore, items);
    }

    // Ghép trạng thái bộ thành một nhãn ngắn, không kèm lý do kiểm duyệt hay nội dung riêng tư.
    private static string BuildSetStatus(bool isPublic, string moderationStatus)
    {
        // 1. Tính giá trị và lưu vào `visibility` để dùng ở bước tiếp theo.
        string visibility = "Riêng tư";
        // 2. Kiểm tra `isPublic` để chọn nhánh xử lý phù hợp.
        if (isPublic)
        {
            // 3. Cập nhật `visibility` bằng giá trị mới.
            visibility = "Công khai";
        }

        // 4. Trả `visibility + " · " + moderationStatus` cho nơi gọi.
        return visibility + " · " + moderationStatus;
    }

    // Cắt phần tử dư được lấy bằng limit + 1 để biết có còn kết quả cho link xem thêm.
    private static bool TrimToLimit(List<AdminGlobalSearchItem> rows, int perTypeLimit)
    {
        // 1. Kiểm tra `rows.Count <= perTypeLimit` để chọn nhánh xử lý phù hợp.
        if (rows.Count <= perTypeLimit)
        {
            // 2. Trả `false` cho nơi gọi.
            return false;
        }

        // 3. Gọi `RemoveAt` để thực hiện bước nghiệp vụ này.
        rows.RemoveAt(rows.Count - 1);
        // 4. Trả `true` cho nơi gọi.
        return true;
    }

    // Chuẩn hóa query: trim, gom khoảng trắng, bỏ ký tự điều khiển và giới hạn độ dài trước khi vào SQL.
    private static NormalizedSearchTerm NormalizeQuery(string? query)
    {
        // 1. Tính giá trị và lưu vào `original` để dùng ở bước tiếp theo.
        string original = string.Empty;
        // 2. Kiểm tra `query != null` để chọn nhánh xử lý phù hợp.
        if (query != null)
        {
            // 3. Cập nhật `original` bằng giá trị mới.
            original = query;
        }

        // 4. Gọi `Replace` và lưu kết quả vào `withoutControlChars`.
        string withoutControlChars = ControlCharacters().Replace(original, " ");
        // 5. Gọi `Replace` và lưu kết quả vào `compacted`.
        string compacted = Whitespace().Replace(withoutControlChars.Trim(), " ");
        // 6. Tính giá trị và lưu vào `wasTruncated` để dùng ở bước tiếp theo.
        bool wasTruncated = compacted.Length > MaxQueryLength;
        // 7. Kiểm tra `wasTruncated` để chọn nhánh xử lý phù hợp.
        if (wasTruncated)
        {
            // 8. Cập nhật `compacted` bằng giá trị mới.
            compacted = compacted[..MaxQueryLength];
        }

        // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new NormalizedSearchTerm(original, compacted, wasTruncated);
    }

    // Nhận các dạng mã an toàn như SET-12, #12 hoặc 12 cho các loại không cần gate nhạy cảm riêng.
    private static int? TryParseCode(string term, string prefix)
    {
        // 1. Gọi `Trim` và lưu kết quả vào `normalized`.
        string normalized = term.Trim();
        // 2. Kiểm tra `normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreC...` để chọn nhánh xử lý phù hợp.
        if (normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
        {
            // 3. Cập nhật `normalized` bằng giá trị mới.
            normalized = normalized[(prefix.Length + 1)..];
        }
        else if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            // 4. Cập nhật `normalized` bằng giá trị mới.
            normalized = normalized[prefix.Length..];
        }
        else if (normalized.StartsWith("#", StringComparison.Ordinal))
        {
            // 5. Cập nhật `normalized` bằng giá trị mới.
            normalized = normalized[1..];
        }

        // 6. Kiểm tra `int.TryParse(normalized, out int id) && id > 0` để chọn nhánh xử lý phù hợp.
        if (int.TryParse(normalized, out int id) && id > 0)
        {
            // 7. Trả `id` cho nơi gọi.
            return id;
        }

        // 8. Trả `null` cho nơi gọi.
        return null;
    }

    // Nhận mã bắt buộc có prefix, dùng cho dữ liệu nhạy cảm để chuỗi số chung không lộ sự tồn tại của mission.
    private static int? TryParseRequiredPrefixedCode(string term, string prefix)
    {
        // 1. Gọi `Trim` và lưu kết quả vào `normalized`.
        string normalized = term.Trim();
        // 2. Kiểm tra `normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreC...` để chọn nhánh xử lý phù hợp.
        if (normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
        {
            // 3. Cập nhật `normalized` bằng giá trị mới.
            normalized = normalized[(prefix.Length + 1)..];
        }
        else if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            // 4. Cập nhật `normalized` bằng giá trị mới.
            normalized = normalized[prefix.Length..];
        }
        else
        {
            // 5. Trả `null` cho nơi gọi.
            return null;
        }

        // 6. Kiểm tra `int.TryParse(normalized, out int id) && id > 0` để chọn nhánh xử lý phù hợp.
        if (int.TryParse(normalized, out int id) && id > 0)
        {
            // 7. Trả `id` cho nơi gọi.
            return id;
        }

        // 8. Trả `null` cho nơi gọi.
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
