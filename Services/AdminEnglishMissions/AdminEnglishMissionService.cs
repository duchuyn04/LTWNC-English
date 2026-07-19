using System.Text.Json;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.AspNetCore.Identity;
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
        _context = context;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    // Tìm danh sách nhiệm vụ ở mức summary; không truy vấn hoặc lọc theo nội dung hội thoại.
    public async Task<AdminEnglishMissionPage> SearchAsync(
        AdminEnglishMissionQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(DefaultPage, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        IQueryable<MissionEntity> missions = _context.EnglishMissions
            .AsNoTracking()
            .Include(mission => mission.StudySession)
                .ThenInclude(session => session!.FlashcardSet);

        missions = ApplyFilters(missions, query, nowUtc);
        int totalCount = await missions.CountAsync(cancellationToken);

        List<MissionEntity> items = await ApplySort(missions, query.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        Dictionary<string, IdentityUser> usersById =
            await LoadUsersByIdAsync(items.Select(mission => mission.StudySession!.UserId), cancellationToken);

        List<AdminEnglishMissionRow> rows = items
            .Select(mission => ToRow(mission, nowUtc, usersById))
            .ToList();

        return new AdminEnglishMissionPage(rows, totalCount, page, pageSize);
    }

    // Mở hội thoại chi tiết sau khi kiểm tra vụ việc, ghi audit thành công rồi mới dựng dữ liệu trả về.
    public async Task<AdminEnglishMissionConversationResult> GetConversationAsync(
        AdminEnglishMissionAccessCommand command,
        CancellationToken cancellationToken = default)
    {
        MissionEntity? mission = await _context.EnglishMissions
            .Include(item => item.StudySession)
                .ThenInclude(session => session!.FlashcardSet)
            .FirstOrDefaultAsync(item => item.Id == command.MissionId, cancellationToken);
        if (mission == null || mission.StudySession == null)
        {
            return new AdminEnglishMissionConversationResult { Found = false };
        }

        string? gateError = ValidateGate(command);
        if (gateError != null)
        {
            return new AdminEnglishMissionConversationResult
            {
                Found = true,
                RequiresGate = true,
                Message = gateError
            };
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        IdentityUser? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == mission.StudySession.UserId, cancellationToken);
        if (mission.ConversationContentDeletedAtUtc != null)
        {
            return new AdminEnglishMissionConversationResult
            {
                Found = true,
                RequiresGate = true,
                Message = "Nội dung hội thoại chi tiết đã hết hạn lưu giữ."
            };
        }

        DateTime retentionDeadlineUtc = CalculateRetentionDeadline(mission);
        if (nowUtc >= retentionDeadlineUtc)
        {
            return new AdminEnglishMissionConversationResult
            {
                Found = true,
                RequiresGate = true,
                Message = "Nội dung hội thoại chi tiết đã quá thời hạn lưu giữ."
            };
        }

        ApplyRetentionHold(mission, command, nowUtc);
        await _auditService.RecordAsync(
            BuildConversationAudit(command, mission, AdminAuditOutcome.Success),
            cancellationToken);
        await _context.Entry(mission)
            .Collection(item => item.TargetWords)
            .LoadAsync(cancellationToken);
        await _context.Entry(mission)
            .Collection(item => item.Turns)
            .LoadAsync(cancellationToken);

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
        int effectiveBatchSize = Math.Clamp(batchSize, 1, DefaultCleanupBatchSize);
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime oldestPossibleExpiryUtc = nowUtc - ConversationDetailRetention;

        List<MissionEntity> candidates = await _context.EnglishMissions
            .Include(mission => mission.Turns)
            .Where(mission => mission.ConversationContentDeletedAtUtc == null
                && mission.CreatedAt <= oldestPossibleExpiryUtc)
            .OrderBy(mission => mission.CreatedAt)
            .Take(effectiveBatchSize)
            .ToListAsync(cancellationToken);

        int clearedCount = 0;
        foreach (MissionEntity mission in candidates)
        {
            DateTime retentionDeadlineUtc = CalculateRetentionDeadline(mission);
            if (nowUtc < retentionDeadlineUtc)
            {
                continue;
            }

            ClearConversationContent(mission, nowUtc);
            clearedCount++;
        }

        if (clearedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new AdminEnglishMissionCleanupResult(candidates.Count, clearedCount);
    }

    // Áp dụng bộ lọc danh sách chỉ trên metadata tổng hợp, không lọc toàn văn hội thoại.
    private IQueryable<MissionEntity> ApplyFilters(
        IQueryable<MissionEntity> missions,
        AdminEnglishMissionQuery query,
        DateTime nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            missions = missions.Where(mission =>
                mission.Title.Contains(term)
                || mission.Topic.Contains(term)
                || (mission.StudySession != null
                    && mission.StudySession.FlashcardSet != null
                    && mission.StudySession.FlashcardSet.Title.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Topic))
        {
            string topic = query.Topic.Trim();
            missions = missions.Where(mission => mission.Topic == topic);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            string status = query.Status.Trim();
            missions = missions.Where(mission => mission.Status == status);
        }

        if (string.Equals(query.Retention, "available", StringComparison.OrdinalIgnoreCase))
        {
            missions = missions.Where(mission =>
                mission.ConversationContentDeletedAtUtc == null);
        }
        else if (string.Equals(query.Retention, "expired", StringComparison.OrdinalIgnoreCase))
        {
            missions = missions.Where(mission =>
                mission.ConversationContentDeletedAtUtc != null
                || mission.CreatedAt <= nowUtc - ConversationDetailRetention);
        }
        else if (string.Equals(query.Retention, "held", StringComparison.OrdinalIgnoreCase))
        {
            missions = missions.Where(mission =>
                mission.ConversationRetentionHoldUntilUtc != null
                && mission.ConversationRetentionHoldUntilUtc > nowUtc);
        }

        return missions;
    }

    // Sắp xếp danh sách theo metadata summary.
    private static IOrderedQueryable<MissionEntity> ApplySort(
        IQueryable<MissionEntity> missions,
        string? sort)
    {
        if (string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase))
        {
            return missions.OrderBy(mission => mission.CreatedAt)
                .ThenBy(mission => mission.Id);
        }

        if (string.Equals(sort, "turns", StringComparison.OrdinalIgnoreCase))
        {
            return missions.OrderByDescending(mission => mission.TurnCount)
                .ThenByDescending(mission => mission.CreatedAt);
        }

        if (string.Equals(sort, "score", StringComparison.OrdinalIgnoreCase))
        {
            return missions.OrderByDescending(mission => mission.Score)
                .ThenByDescending(mission => mission.CreatedAt);
        }

        return missions.OrderByDescending(mission => mission.CreatedAt)
            .ThenByDescending(mission => mission.Id);
    }

    // Kiểm tra cổng lý do/vụ việc trước khi service đọc hội thoại.
    private static string? ValidateGate(AdminEnglishMissionAccessCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.IncidentType))
        {
            return "Loại vụ việc là bắt buộc.";
        }

        if (!AllowedIncidentTypes.Contains(command.IncidentType.Trim()))
        {
            return "Loại vụ việc không hợp lệ.";
        }

        if (command.CaseReference?.Length > 120)
        {
            return "Mã tham chiếu tối đa 120 ký tự.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return "Lý do mở hội thoại là bắt buộc.";
        }

        if (command.Reason.Trim().Length < 10)
        {
            return "Lý do mở hội thoại cần ít nhất 10 ký tự.";
        }

        return null;
    }

    // Tạo hoặc gia hạn mốc tạm giữ vụ việc, nhưng không vượt quá 12 tháng từ ngày mission được tạo.
    private static void ApplyRetentionHold(
        MissionEntity mission,
        AdminEnglishMissionAccessCommand command,
        DateTime nowUtc)
    {
        DateTime maximumHoldUtc = mission.CreatedAt + IncidentHoldLimit;
        DateTime requestedHoldUtc = nowUtc + ConversationDetailRetention;
        DateTime nextHoldUtc = requestedHoldUtc;
        if (nextHoldUtc > maximumHoldUtc)
        {
            nextHoldUtc = maximumHoldUtc;
        }

        if (mission.ConversationRetentionHoldUntilUtc == null
            || mission.ConversationRetentionHoldUntilUtc < nextHoldUtc)
        {
            mission.ConversationRetentionHoldUntilUtc = nextHoldUtc;
        }

        mission.ConversationRetentionCaseType = command.IncidentType.Trim();
        mission.ConversationRetentionCaseReference = TrimOrNull(command.CaseReference);
    }

    // Tính deadline xóa nội dung: mặc định 90 ngày, có hold thì không vượt quá 12 tháng.
    private static DateTime CalculateRetentionDeadline(MissionEntity mission)
    {
        DateTime defaultDeadlineUtc = mission.CreatedAt + ConversationDetailRetention;
        DateTime maximumDeadlineUtc = mission.CreatedAt + IncidentHoldLimit;
        DateTime deadlineUtc = defaultDeadlineUtc;

        if (mission.ConversationRetentionHoldUntilUtc != null
            && mission.ConversationRetentionHoldUntilUtc > deadlineUtc)
        {
            deadlineUtc = mission.ConversationRetentionHoldUntilUtc.Value;
        }

        if (deadlineUtc > maximumDeadlineUtc)
        {
            return maximumDeadlineUtc;
        }

        return deadlineUtc;
    }

    // Xóa các trường nội dung hội thoại chi tiết; giữ số lượt, điểm, trạng thái và aggregate JSON.
    private static void ClearConversationContent(
        MissionEntity mission,
        DateTime deletedAtUtc)
    {
        foreach (EnglishMissionTurn turn in mission.Turns)
        {
            turn.UserText = string.Empty;
            turn.NpcText = string.Empty;
            turn.FeedbackVi = null;
            turn.CorrectionEn = null;
            turn.CorrectionExplanationVi = null;
            turn.ProviderName = null;
            turn.ModelId = null;
        }

        mission.Situation = string.Empty;
        mission.OpeningLine = string.Empty;
        mission.ConversationContentDeletedAtUtc = deletedAtUtc;
    }

    // Chuyển entity sang dòng summary, không mang theo text hội thoại.
    private static AdminEnglishMissionRow ToRow(
        MissionEntity mission,
        DateTime nowUtc,
        IReadOnlyDictionary<string, IdentityUser> usersById)
    {
        StudySession session = mission.StudySession!;
        FlashcardSet? set = session.FlashcardSet;
        IdentityUser? user = null;
        usersById.TryGetValue(session.UserId, out user);
        DateTime retentionDeadlineUtc = CalculateRetentionDeadline(mission);
        bool available = mission.ConversationContentDeletedAtUtc == null
            && nowUtc < retentionDeadlineUtc;
        bool hasHold = mission.ConversationRetentionHoldUntilUtc != null
            && mission.ConversationRetentionHoldUntilUtc > nowUtc;

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
        IdentityUser? user,
        AdminEnglishMissionAccessCommand command,
        DateTime retentionDeadlineUtc)
    {
        StudySession session = mission.StudySession!;
        FlashcardSet? set = session.FlashcardSet;

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
        Dictionary<string, string?> metadata = new()
        {
            ["incidentType"] = command.IncidentType.Trim(),
            ["caseReference"] = TrimOrNull(command.CaseReference),
            ["status"] = mission.Status,
            ["topic"] = mission.Topic,
            ["turnCount"] = mission.TurnCount.ToString()
        };

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
        try
        {
            string[]? values = JsonSerializer.Deserialize<string[]>(json);
            if (values == null || values.Length == 0)
            {
                return "—";
            }

            return string.Join(", ", values);
        }
        catch (JsonException)
        {
            return "—";
        }
    }

    // Chuẩn hóa chuỗi tùy chọn để tránh lưu khoảng trắng thừa.
    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    // Tải thông tin Identity cho các phiên trên trang hiện tại để hiển thị email/tên người học.
    private async Task<Dictionary<string, IdentityUser>> LoadUsersByIdAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        string[] ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, IdentityUser>(StringComparer.Ordinal);
        }

        List<IdentityUser> users = await _context.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(user => user.Id, StringComparer.Ordinal);
    }
}
