using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;
using MissionEntity = ltwnc.Models.Entities.EnglishMission;

namespace ltwnc.Services.AdminEnglishMissions;

public sealed class AdminEnglishMissionService : IAdminEnglishMissionService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int DefaultCleanupBatchSize = 100;
    public static readonly TimeSpan ConversationDetailRetention = TimeSpan.FromDays(90);
    public static readonly TimeSpan IncidentHoldLimit = TimeSpan.FromDays(365);

    private static readonly HashSet<string> AllowedIncidentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "support",
            "report",
            "safety",
            "quality"
        };

    private readonly AppDbContext _context;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    // Service quản trị chỉ đọc hội thoại theo cổng vụ việc và dọn nội dung hết hạn.
    public AdminEnglishMissionService(
        AppDbContext context,
        IAdminAuditService auditService,
        TimeProvider timeProvider)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_auditService` để các phương thức khác sử dụng.
        _auditService = auditService;
        // 3. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    // Tìm danh sách nhiệm vụ ở mức summary; không truy vấn hoặc lọc theo nội dung hội thoại.
    public async Task<AdminEnglishMissionPage> SearchAsync(
        AdminEnglishMissionQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Max` và lưu kết quả vào `page`.
        int page = Math.Max(DefaultPage, query.Page);
        // 2. Gọi `Clamp` và lưu kết quả vào `pageSize`.
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        // 3. Tính giá trị và lưu vào `nowUtc` để dùng ở bước tiếp theo.
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // 4. Gọi `ThenInclude` và lưu kết quả vào `missions`.
        IQueryable<MissionEntity> missions = _context.EnglishMissions
            .AsNoTracking()
            .Include(mission => mission.StudySession)
                .ThenInclude(session => session!.FlashcardSet);

        // 5. Cập nhật `missions` bằng giá trị mới.
        missions = ApplyFilters(missions, query, nowUtc);
        // 6. Gọi `CountAsync` và lưu kết quả vào `totalCount`.
        int totalCount = await missions.CountAsync(cancellationToken);

        // 7. Gọi `ToListAsync` và lưu kết quả vào `items`.
        List<MissionEntity> items = await ApplySort(missions, query.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        // 8. Gọi `LoadUsersByIdAsync` và lưu kết quả vào `usersById`.
        Dictionary<string, AppUser> usersById =
            await LoadUsersByIdAsync(items.Select(mission => mission.StudySession!.UserId), cancellationToken);

        // 9. Gọi `ToList` và lưu kết quả vào `rows`.
        List<AdminEnglishMissionRow> rows = items
            .Select(mission => ToRow(mission, nowUtc, usersById))
            .ToList();

        // 10. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminEnglishMissionPage(rows, totalCount, page, pageSize);
    }

    // Mở hội thoại chi tiết sau khi kiểm tra vụ việc, ghi audit thành công rồi mới dựng dữ liệu trả về.
    public async Task<AdminEnglishMissionConversationResult> GetConversationAsync(
        AdminEnglishMissionAccessCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `mission`.
        MissionEntity? mission = await _context.EnglishMissions
            .Include(item => item.StudySession)
                .ThenInclude(session => session!.FlashcardSet)
            .FirstOrDefaultAsync(item => item.Id == command.MissionId, cancellationToken);
        // 2. Kiểm tra `mission == null || mission.StudySession == null` để chọn nhánh xử lý phù hợp.
        if (mission == null || mission.StudySession == null)
        {
            // 3. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AdminEnglishMissionConversationResult { Found = false };
        }

        // 4. Gọi `ValidateGate` và lưu kết quả vào `gateError`.
        string? gateError = ValidateGate(command);
        // 5. Kiểm tra `gateError != null` để chọn nhánh xử lý phù hợp.
        if (gateError != null)
        {
            // 6. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AdminEnglishMissionConversationResult
            {
                Found = true,
                RequiresGate = true,
                Message = gateError
            };
        }

        // 7. Tính giá trị và lưu vào `nowUtc` để dùng ở bước tiếp theo.
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        // 8. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `user`.
        AppUser? user = await _context.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.StudySession.UserId, cancellationToken);
        // 9. Kiểm tra `mission.ConversationContentDeletedAtUtc != null` để chọn nhánh xử lý phù hợp.
        if (mission.ConversationContentDeletedAtUtc != null)
        {
            // 10. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AdminEnglishMissionConversationResult
            {
                Found = true,
                RequiresGate = true,
                Message = "Nội dung hội thoại chi tiết đã hết hạn lưu giữ."
            };
        }

        // 11. Gọi `CalculateRetentionDeadline` và lưu kết quả vào `retentionDeadlineUtc`.
        DateTime retentionDeadlineUtc = CalculateRetentionDeadline(mission);
        // 12. Kiểm tra `nowUtc >= retentionDeadlineUtc` để chọn nhánh xử lý phù hợp.
        if (nowUtc >= retentionDeadlineUtc)
        {
            // 13. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AdminEnglishMissionConversationResult
            {
                Found = true,
                RequiresGate = true,
                Message = "Nội dung hội thoại chi tiết đã quá thời hạn lưu giữ."
            };
        }

        // 14. Gọi `ApplyRetentionHold` để thực hiện bước nghiệp vụ này.
        ApplyRetentionHold(mission, command, nowUtc);
        // 15. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
        await _auditService.RecordAsync(
            BuildConversationAudit(command, mission, AdminAuditOutcome.Success),
            cancellationToken);
        // 16. Gọi `LoadAsync` để thực hiện bước nghiệp vụ này.
        await _context.Entry(mission)
            .Collection(item => item.TargetWords)
            .LoadAsync(cancellationToken);
        // 17. Gọi `LoadAsync` để thực hiện bước nghiệp vụ này.
        await _context.Entry(mission)
            .Collection(item => item.Turns)
            .LoadAsync(cancellationToken);

        // 18. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminEnglishMissionConversationResult
        {
            Found = true,
            Conversation = ToConversation(
                mission,
                user,
                command,
                CalculateRetentionDeadline(mission))
        };
    }

    // Dọn nội dung hội thoại theo batch nhỏ, chạy lặp an toàn và không ghi nội dung bị xóa vào audit/log.
    public async Task<AdminEnglishMissionCleanupResult> CleanupExpiredConversationContentAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Clamp` và lưu kết quả vào `effectiveBatchSize`.
        int effectiveBatchSize = Math.Clamp(batchSize, 1, DefaultCleanupBatchSize);
        // 2. Tính giá trị và lưu vào `nowUtc` để dùng ở bước tiếp theo.
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        // 3. Tính giá trị và lưu vào `oldestPossibleExpiryUtc` để dùng ở bước tiếp theo.
        DateTime oldestPossibleExpiryUtc = nowUtc - ConversationDetailRetention;
        // 4. Tính giá trị và lưu vào `maximumRetentionStartUtc` để dùng ở bước tiếp theo.
        DateTime maximumRetentionStartUtc = nowUtc - IncidentHoldLimit;

        // Lọc bản ghi thực sự đến hạn trước khi giới hạn batch để mission đang hold không che dữ liệu phía sau.
        // 5. Gọi `ToListAsync` và lưu kết quả vào `candidates`.
        List<MissionEntity> candidates = await _context.EnglishMissions
            .Include(mission => mission.Turns)
            .Where(mission => mission.ConversationContentDeletedAtUtc == null
                && mission.CreatedAt <= oldestPossibleExpiryUtc
                && (mission.ConversationRetentionHoldUntilUtc == null
                    || mission.ConversationRetentionHoldUntilUtc <= nowUtc
                    || mission.CreatedAt <= maximumRetentionStartUtc))
            .OrderBy(mission => mission.CreatedAt)
            .Take(effectiveBatchSize)
            .ToListAsync(cancellationToken);

        // 6. Tính giá trị và lưu vào `clearedCount` để dùng ở bước tiếp theo.
        int clearedCount = 0;
        // 7. Duyệt từng `mission` trong `candidates` để xử lý lần lượt.
        foreach (MissionEntity mission in candidates)
        {
            // 8. Gọi `CalculateRetentionDeadline` và lưu kết quả vào `retentionDeadlineUtc`.
            DateTime retentionDeadlineUtc = CalculateRetentionDeadline(mission);
            // 9. Kiểm tra `nowUtc < retentionDeadlineUtc` để chọn nhánh xử lý phù hợp.
            if (nowUtc < retentionDeadlineUtc)
            {
                // 10. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                continue;
            }

            // 11. Gọi `ClearConversationContent` để thực hiện bước nghiệp vụ này.
            ClearConversationContent(mission, nowUtc);
            // 12. Cập nhật bộ đếm hoặc trạng thái `clearedCount`.
            clearedCount++;
        }

        // 13. Kiểm tra `clearedCount > 0` để chọn nhánh xử lý phù hợp.
        if (clearedCount > 0)
        {
            // 14. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 15. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminEnglishMissionCleanupResult(candidates.Count, clearedCount);
    }

    // Áp dụng bộ lọc danh sách chỉ trên metadata tổng hợp, không lọc toàn văn hội thoại.
    private IQueryable<MissionEntity> ApplyFilters(
        IQueryable<MissionEntity> missions,
        AdminEnglishMissionQuery query,
        DateTime nowUtc)
    {
        // 1. Kiểm tra `!string.IsNullOrWhiteSpace(query.Search)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // 2. Gọi `Trim` và lưu kết quả vào `term`.
            string term = query.Search.Trim();
            // 3. Cập nhật `missions` bằng giá trị mới.
            missions = missions.Where(mission =>
                mission.Title.Contains(term)
                || mission.Topic.Contains(term)
                || (mission.StudySession != null
                    && mission.StudySession.FlashcardSet != null
                    && mission.StudySession.FlashcardSet.Title.Contains(term)));
        }

        // 4. Kiểm tra `!string.IsNullOrWhiteSpace(query.Topic)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Topic))
        {
            // 5. Gọi `Trim` và lưu kết quả vào `topic`.
            string topic = query.Topic.Trim();
            // 6. Cập nhật `missions` bằng giá trị mới.
            missions = missions.Where(mission => mission.Topic == topic);
        }

        // 7. Kiểm tra `!string.IsNullOrWhiteSpace(query.Status)` để chọn nhánh xử lý phù hợp.
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            // 8. Gọi `Trim` và lưu kết quả vào `status`.
            string status = query.Status.Trim();
            // 9. Cập nhật `missions` bằng giá trị mới.
            missions = missions.Where(mission => mission.Status == status);
        }

        // 10. Kiểm tra `string.Equals(query.Retention, "available", StringComparison.Ordina...` để chọn nhánh xử lý phù hợp.
        if (string.Equals(query.Retention, "available", StringComparison.OrdinalIgnoreCase))
        {
            // 11. Cập nhật `missions` bằng giá trị mới.
            missions = missions.Where(mission =>
                mission.ConversationContentDeletedAtUtc == null);
        }
        else if (string.Equals(query.Retention, "expired", StringComparison.OrdinalIgnoreCase))
        {
            // 12. Cập nhật `missions` bằng giá trị mới.
            missions = missions.Where(mission =>
                mission.ConversationContentDeletedAtUtc != null
                || mission.CreatedAt <= nowUtc - ConversationDetailRetention);
        }
        else if (string.Equals(query.Retention, "held", StringComparison.OrdinalIgnoreCase))
        {
            // 13. Cập nhật `missions` bằng giá trị mới.
            missions = missions.Where(mission =>
                mission.ConversationRetentionHoldUntilUtc != null
                && mission.ConversationRetentionHoldUntilUtc > nowUtc);
        }

        // 14. Trả `missions` cho nơi gọi.
        return missions;
    }

    // Sắp xếp danh sách theo metadata summary.
    private static IOrderedQueryable<MissionEntity> ApplySort(
        IQueryable<MissionEntity> missions,
        string? sort)
    {
        // 1. Kiểm tra `string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase)` để chọn nhánh xử lý phù hợp.
        if (string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase))
        {
            // 2. Trả kết quả từ `ThenBy` cho nơi gọi.
            return missions.OrderBy(mission => mission.CreatedAt)
                .ThenBy(mission => mission.Id);
        }

        // 3. Kiểm tra `string.Equals(sort, "turns", StringComparison.OrdinalIgnoreCase)` để chọn nhánh xử lý phù hợp.
        if (string.Equals(sort, "turns", StringComparison.OrdinalIgnoreCase))
        {
            // 4. Trả kết quả từ `ThenByDescending` cho nơi gọi.
            return missions.OrderByDescending(mission => mission.TurnCount)
                .ThenByDescending(mission => mission.CreatedAt);
        }

        // 5. Kiểm tra `string.Equals(sort, "score", StringComparison.OrdinalIgnoreCase)` để chọn nhánh xử lý phù hợp.
        if (string.Equals(sort, "score", StringComparison.OrdinalIgnoreCase))
        {
            // 6. Trả kết quả từ `ThenByDescending` cho nơi gọi.
            return missions.OrderByDescending(mission => mission.Score)
                .ThenByDescending(mission => mission.CreatedAt);
        }

        // 7. Trả kết quả từ `ThenByDescending` cho nơi gọi.
        return missions.OrderByDescending(mission => mission.CreatedAt)
            .ThenByDescending(mission => mission.Id);
    }

    // Kiểm tra cổng lý do/vụ việc trước khi service đọc hội thoại.
    private static string? ValidateGate(AdminEnglishMissionAccessCommand command)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(command.IncidentType)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.IncidentType))
        {
            // 2. Trả `"Loại vụ việc là bắt buộc."` cho nơi gọi.
            return "Loại vụ việc là bắt buộc.";
        }

        // 3. Kiểm tra `!AllowedIncidentTypes.Contains(command.IncidentType.Trim())` để chọn nhánh xử lý phù hợp.
        if (!AllowedIncidentTypes.Contains(command.IncidentType.Trim()))
        {
            // 4. Trả `"Loại vụ việc không hợp lệ."` cho nơi gọi.
            return "Loại vụ việc không hợp lệ.";
        }

        // 5. Kiểm tra `command.CaseReference?.Length > 120` để chọn nhánh xử lý phù hợp.
        if (command.CaseReference?.Length > 120)
        {
            // 6. Trả `"Mã tham chiếu tối đa 120 ký tự."` cho nơi gọi.
            return "Mã tham chiếu tối đa 120 ký tự.";
        }

        // 7. Kiểm tra `string.IsNullOrWhiteSpace(command.Reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            // 8. Trả `"Lý do mở hội thoại là bắt buộc."` cho nơi gọi.
            return "Lý do mở hội thoại là bắt buộc.";
        }

        // 9. Kiểm tra `command.Reason.Trim().Length < 10` để chọn nhánh xử lý phù hợp.
        if (command.Reason.Trim().Length < 10)
        {
            // 10. Trả `"Lý do mở hội thoại cần ít nhất 10 ký tự."` cho nơi gọi.
            return "Lý do mở hội thoại cần ít nhất 10 ký tự.";
        }

        // 11. Trả `null` cho nơi gọi.
        return null;
    }

    // Tạo hoặc gia hạn mốc tạm giữ vụ việc, nhưng không vượt quá 12 tháng từ ngày mission được tạo.
    private static void ApplyRetentionHold(
        MissionEntity mission,
        AdminEnglishMissionAccessCommand command,
        DateTime nowUtc)
    {
        // 1. Tính giá trị và lưu vào `maximumHoldUtc` để dùng ở bước tiếp theo.
        DateTime maximumHoldUtc = mission.CreatedAt + IncidentHoldLimit;
        // 2. Tính giá trị và lưu vào `requestedHoldUtc` để dùng ở bước tiếp theo.
        DateTime requestedHoldUtc = nowUtc + ConversationDetailRetention;
        // 3. Tính giá trị và lưu vào `nextHoldUtc` để dùng ở bước tiếp theo.
        DateTime nextHoldUtc = requestedHoldUtc;
        // 4. Kiểm tra `nextHoldUtc > maximumHoldUtc` để chọn nhánh xử lý phù hợp.
        if (nextHoldUtc > maximumHoldUtc)
        {
            // 5. Cập nhật `nextHoldUtc` bằng giá trị mới.
            nextHoldUtc = maximumHoldUtc;
        }

        // 6. Kiểm tra `mission.ConversationRetentionHoldUntilUtc == null || mission.Conver...` để chọn nhánh xử lý phù hợp.
        if (mission.ConversationRetentionHoldUntilUtc == null
            || mission.ConversationRetentionHoldUntilUtc < nextHoldUtc)
        {
            // 7. Cập nhật `mission.ConversationRetentionHoldUntilUtc` bằng giá trị mới.
            mission.ConversationRetentionHoldUntilUtc = nextHoldUtc;
        }

        // 8. Cập nhật `mission.ConversationRetentionCaseType` bằng giá trị mới.
        mission.ConversationRetentionCaseType = command.IncidentType.Trim();
        // 9. Cập nhật `mission.ConversationRetentionCaseReference` bằng giá trị mới.
        mission.ConversationRetentionCaseReference = TrimOrNull(command.CaseReference);
    }

    // Tính deadline xóa nội dung: mặc định 90 ngày, có hold thì không vượt quá 12 tháng.
    private static DateTime CalculateRetentionDeadline(MissionEntity mission)
    {
        // 1. Tính giá trị và lưu vào `defaultDeadlineUtc` để dùng ở bước tiếp theo.
        DateTime defaultDeadlineUtc = mission.CreatedAt + ConversationDetailRetention;
        // 2. Tính giá trị và lưu vào `maximumDeadlineUtc` để dùng ở bước tiếp theo.
        DateTime maximumDeadlineUtc = mission.CreatedAt + IncidentHoldLimit;
        // 3. Tính giá trị và lưu vào `deadlineUtc` để dùng ở bước tiếp theo.
        DateTime deadlineUtc = defaultDeadlineUtc;

        // 4. Kiểm tra `mission.ConversationRetentionHoldUntilUtc != null && mission.Conver...` để chọn nhánh xử lý phù hợp.
        if (mission.ConversationRetentionHoldUntilUtc != null
            && mission.ConversationRetentionHoldUntilUtc > deadlineUtc)
        {
            // 5. Cập nhật `deadlineUtc` bằng giá trị mới.
            deadlineUtc = mission.ConversationRetentionHoldUntilUtc.Value;
        }

        // 6. Kiểm tra `deadlineUtc > maximumDeadlineUtc` để chọn nhánh xử lý phù hợp.
        if (deadlineUtc > maximumDeadlineUtc)
        {
            // 7. Trả `maximumDeadlineUtc` cho nơi gọi.
            return maximumDeadlineUtc;
        }

        // 8. Trả `deadlineUtc` cho nơi gọi.
        return deadlineUtc;
    }

    // Xóa các trường nội dung hội thoại chi tiết; giữ số lượt, điểm, trạng thái và aggregate JSON.
    private static void ClearConversationContent(
        MissionEntity mission,
        DateTime deletedAtUtc)
    {
        // 1. Duyệt từng `turn` trong `mission.Turns` để xử lý lần lượt.
        foreach (EnglishMissionTurn turn in mission.Turns)
        {
            // 2. Cập nhật `turn.UserText` bằng giá trị mới.
            turn.UserText = string.Empty;
            // 3. Cập nhật `turn.NpcText` bằng giá trị mới.
            turn.NpcText = string.Empty;
            // 4. Cập nhật `turn.FeedbackVi` bằng giá trị mới.
            turn.FeedbackVi = null;
            // 5. Cập nhật `turn.CorrectionEn` bằng giá trị mới.
            turn.CorrectionEn = null;
            // 6. Cập nhật `turn.CorrectionExplanationVi` bằng giá trị mới.
            turn.CorrectionExplanationVi = null;
            // 7. Cập nhật `turn.ProviderName` bằng giá trị mới.
            turn.ProviderName = null;
            // 8. Cập nhật `turn.ModelId` bằng giá trị mới.
            turn.ModelId = null;
        }

        // 9. Cập nhật `mission.Situation` bằng giá trị mới.
        mission.Situation = string.Empty;
        // 10. Cập nhật `mission.OpeningLine` bằng giá trị mới.
        mission.OpeningLine = string.Empty;
        // 11. Cập nhật `mission.ConversationContentDeletedAtUtc` bằng giá trị mới.
        mission.ConversationContentDeletedAtUtc = deletedAtUtc;
    }

    // Chuyển entity sang dòng summary, không mang theo text hội thoại.
    private static AdminEnglishMissionRow ToRow(
        MissionEntity mission,
        DateTime nowUtc,
        IReadOnlyDictionary<string, AppUser> usersById)
    {
        // 1. Tính giá trị và lưu vào `session` để dùng ở bước tiếp theo.
        StudySession session = mission.StudySession!;
        // 2. Tính giá trị và lưu vào `set` để dùng ở bước tiếp theo.
        FlashcardSet? set = session.FlashcardSet;
        // 3. Tính giá trị và lưu vào `user` để dùng ở bước tiếp theo.
        AppUser? user = null;
        // 4. Gọi `TryGetValue` để thực hiện bước nghiệp vụ này.
        usersById.TryGetValue(session.UserId, out user);
        // 5. Gọi `CalculateRetentionDeadline` và lưu kết quả vào `retentionDeadlineUtc`.
        DateTime retentionDeadlineUtc = CalculateRetentionDeadline(mission);
        // 6. Tính giá trị và lưu vào `available` để dùng ở bước tiếp theo.
        bool available = mission.ConversationContentDeletedAtUtc == null
            && nowUtc < retentionDeadlineUtc;
        // 7. Tính giá trị và lưu vào `hasHold` để dùng ở bước tiếp theo.
        bool hasHold = mission.ConversationRetentionHoldUntilUtc != null
            && mission.ConversationRetentionHoldUntilUtc > nowUtc;

        // 8. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminEnglishMissionRow(
            mission.Id,
            mission.StudySessionId,
            user?.UserName ?? session.UserId,
            user?.Email ?? session.UserId,
            session.FlashcardSetId,
            set?.Title ?? $"Bộ thẻ #{session.FlashcardSetId}",
            mission.Topic,
            mission.Title,
            mission.Status,
            mission.TurnCount,
            mission.Score,
            mission.CreatedAt,
            mission.CompletedAt,
            retentionDeadlineUtc,
            available,
            hasHold);
    }

    // Dựng dữ liệu hội thoại đã lọc cho Admin, không gồm ProviderName/ModelId hay chi tiết vận hành AI.
    private static AdminEnglishMissionConversation ToConversation(
        MissionEntity mission,
        AppUser? user,
        AdminEnglishMissionAccessCommand command,
        DateTime retentionDeadlineUtc)
    {
        // 1. Tính giá trị và lưu vào `session` để dùng ở bước tiếp theo.
        StudySession session = mission.StudySession!;
        // 2. Tính giá trị và lưu vào `set` để dùng ở bước tiếp theo.
        FlashcardSet? set = session.FlashcardSet;

        // 3. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminEnglishMissionConversation(
            mission.Id,
            mission.StudySessionId,
            user?.UserName ?? session.UserId,
            user?.Email ?? session.UserId,
            set?.Title ?? $"Bộ thẻ #{session.FlashcardSetId}",
            mission.Topic,
            mission.Title,
            mission.Situation,
            mission.NpcName,
            mission.NpcRole,
            mission.OpeningLine,
            mission.Status,
            mission.TurnCount,
            mission.Score,
            mission.CreatedAt,
            mission.CompletedAt,
            retentionDeadlineUtc,
            command.IncidentType.Trim(),
            TrimOrNull(command.CaseReference),
            command.Reason.Trim(),
            mission.TargetWords
                .OrderBy(word => word.Id)
                .Select(ToTargetWordRow)
                .ToList(),
            mission.Turns
                .OrderBy(turn => turn.TurnNumber)
                .Select(ToTurnRow)
                .ToList());
    }

    // Dựng dòng từ mục tiêu cho trang chi tiết.
    private static AdminEnglishMissionTargetWordRow ToTargetWordRow(
        EnglishMissionTargetWord word)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminEnglishMissionTargetWordRow(
            word.Term,
            word.Definition,
            word.PartOfSpeech,
            word.IsUsed,
            word.FirstUsedTurn);
    }

    // Dựng dòng hội thoại đã loại bỏ metadata provider.
    private static AdminEnglishMissionTurnRow ToTurnRow(EnglishMissionTurn turn)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminEnglishMissionTurnRow(
            turn.TurnNumber,
            turn.UserText,
            turn.NpcText,
            turn.FeedbackVi,
            turn.CorrectionEn,
            turn.CorrectionExplanationVi,
            JoinJsonArray(turn.UsedWordsJson),
            JoinJsonArray(turn.AchievedGoalsJson),
            turn.CreatedAt);
    }

    // Ghi audit truy cập hội thoại, chỉ metadata vụ việc và summary, không ghi nội dung hội thoại.
    private static AdminAuditEntry BuildConversationAudit(
        AdminEnglishMissionAccessCommand command,
        MissionEntity mission,
        string outcome)
    {
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        Dictionary<string, string?> metadata = new()
        {
            ["incidentType"] = command.IncidentType.Trim(),
            ["caseReference"] = TrimOrNull(command.CaseReference),
            ["status"] = mission.Status,
            ["topic"] = mission.Topic,
            ["turnCount"] = mission.TurnCount.ToString()
        };

        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAuditEntry(
            command.ActorUserId,
            command.ActorDisplay,
            AdminAuditActions.EnglishMissionsViewConversation,
            outcome,
            TargetType: "EnglishMission",
            TargetId: mission.Id.ToString(),
            Reason: command.Reason,
            CorrelationId: command.CorrelationId,
            Metadata: metadata);
    }

    // Chuyển JSON array aggregate thành chuỗi đọc được; lỗi dữ liệu thì trả rỗng thay vì lộ exception.
    private static string JoinJsonArray(string json)
    {
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 2. Gọi `Deserialize` và lưu kết quả vào `values`.
            string[]? values = JsonSerializer.Deserialize<string[]>(json);
            // 3. Kiểm tra `values == null || values.Length == 0` để chọn nhánh xử lý phù hợp.
            if (values == null || values.Length == 0)
            {
                // 4. Trả `"—"` cho nơi gọi.
                return "—";
            }

            // 5. Trả kết quả từ `Join` cho nơi gọi.
            return string.Join(", ", values);
        }
        catch (JsonException)
        {
            // 6. Trả `"—"` cho nơi gọi.
            return "—";
        }
    }

    // Chuẩn hóa chuỗi tùy chọn để tránh lưu khoảng trắng thừa.
    private static string? TrimOrNull(string? value)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(value))
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Trả kết quả từ `Trim` cho nơi gọi.
        return value.Trim();
    }

    // Tải thông tin tài khoản cho các phiên trên trang hiện tại để hiển thị email/tên người học.
    private async Task<Dictionary<string, AppUser>> LoadUsersByIdAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `ToArray` và lưu kết quả vào `ids`.
        string[] ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        // 2. Kiểm tra `ids.Length == 0` để chọn nhánh xử lý phù hợp.
        if (ids.Length == 0)
        {
            // 3. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new Dictionary<string, AppUser>(StringComparer.Ordinal);
        }

        // 4. Gọi `ToListAsync` và lưu kết quả vào `users`.
        List<AppUser> users = await _context.AppUsers
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);

        // 5. Trả kết quả từ `ToDictionary` cho nơi gọi.
        return users.ToDictionary(user => user.Id, StringComparer.Ordinal);
    }
}
