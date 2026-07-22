using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Achievements;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.AdminAchievements;

// Quản trị thành tích ở chế độ chỉ đọc và tính lại bằng AchievementUnlockService hiện có.
public sealed class AdminAchievementService : IAdminAchievementService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int DefaultBatchSize = 50;
    public const int MaxBatchSize = 200;
    private const int MaxReasonLength = 500;

    private readonly AppDbContext _context;
    private readonly IAchievementProgressService _progressService;
    private readonly IAchievementUnlockService _unlockService;
    private readonly IAdminAuditService _auditService;
    private readonly AdminAchievementSyncCoordinator _syncCoordinator;

    // Nhận các dependency đọc/ghi cần thiết để controller không tự tính thành tích.
    public AdminAchievementService(
        AppDbContext context,
        IAchievementProgressService progressService,
        IAchievementUnlockService unlockService,
        IAdminAuditService auditService,
        AdminAchievementSyncCoordinator syncCoordinator)
    {
        _context = context;
        _progressService = progressService;
        _unlockService = unlockService;
        _auditService = auditService;
        _syncCoordinator = syncCoordinator;
    }

    // Lấy danh mục từ mã nguồn, đếm người đã nhận và tính kết quả cho user trên trang hiện tại.
    public async Task<AdminAchievementOverview> GetOverviewAsync(
        AdminAchievementQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(DefaultPage, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        Dictionary<string, int> recipientCounts =
            await LoadRecipientCountsAsync(cancellationToken);
        IReadOnlyList<AdminAchievementDefinitionSummary> catalog =
            BuildCatalogSummaries(recipientCounts);

        IQueryable<AppUser> users = ApplySearch(
            _context.AppUsers.AsNoTracking(),
            query.Search);
        int totalUsers = await users.CountAsync(cancellationToken);
        List<AppUser> pageUsers = await users
            .OrderBy(user => user.Email)
            .ThenBy(user => user.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        IReadOnlyList<AdminAchievementUserResult> results =
            await BuildUserResultsAsync(pageUsers, cancellationToken);

        return new AdminAchievementOverview(catalog, results, totalUsers, page, pageSize);
    }

    // Đồng bộ một user với khóa chống chạy trùng, transaction và audit trong cùng kết quả nghiệp vụ.
    public async Task<AdminAchievementSyncResult> ResyncUserAsync(
        AdminAchievementSyncCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationError = ValidateSyncCommand(command);
        if (validationError != null)
        {
            return AdminAchievementSyncResult.Failure(validationError, 0);
        }

        AppUser? target = await _context.AppUsers.SingleOrDefaultAsync(
            item => item.Id == command.TargetUserId,
            cancellationToken);
        if (target == null)
        {
            return AdminAchievementSyncResult.Failure("Không tìm thấy người dùng cần đồng bộ.");
        }

        using IDisposable? lease = _syncCoordinator.TryStartUser(command.TargetUserId);
        if (lease == null)
        {
            await RecordDeniedAuditAsync(
                command,
                target,
                "Tác vụ đồng bộ thành tích cho người dùng này đang chạy.",
                cancellationToken);
            return AdminAchievementSyncResult.Failure("Đang có tác vụ đồng bộ thành tích cho phạm vi này. Vui lòng thử lại sau.");
        }

        return await RunUserSyncAsync(
            command,
            target,
            "single-user",
            cancellationToken);
    }

    // Đồng bộ toàn hệ thống theo lô nhỏ; mỗi user có transaction riêng để lỗi không để lại nửa trạng thái.
    public async Task<AdminAchievementBatchSyncResult> ResyncAllAsync(
        AdminAchievementBatchSyncCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationError = ValidateBatchCommand(command);
        if (validationError != null)
        {
            return AdminAchievementBatchSyncResult.Failure(validationError);
        }

        using IDisposable? lease = _syncCoordinator.TryStartSystem();
        if (lease == null)
        {
            await RecordSystemAuditAsync(
                command,
                AdminAuditOutcome.Denied,
                0,
                0,
                1,
                "duplicate",
                cancellationToken);
            return AdminAchievementBatchSyncResult.Failure("Đang có tác vụ đồng bộ thành tích khác chạy. Vui lòng thử lại sau.");
        }

        int processedUsers = 0;
        int changedCount = 0;
        int failedCount = 0;
        int batchSize = NormalizeBatchSize(command.BatchSize);
        string? lastProcessedUserId = null;
        while (true)
        {
            IQueryable<AppUser> userBatchQuery = _context.AppUsers.AsNoTracking();
            if (lastProcessedUserId != null)
            {
                // Keyset theo Id giữ vị trí ổn định khi có tài khoản mới xuất hiện giữa hai batch.
                userBatchQuery = userBatchQuery.Where(user =>
                    string.Compare(user.Id, lastProcessedUserId) > 0);
            }

            // Chỉ đọc một trang user từ database để không giữ toàn bộ mã tài khoản trong bộ nhớ.
            List<AppUser> users = await userBatchQuery
                .OrderBy(user => user.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            if (users.Count == 0)
            {
                break;
            }

            foreach (AppUser user in users)
            {
                AdminAchievementSyncResult result = await RunUserSyncAsync(
                    new AdminAchievementSyncCommand(
                        command.ActorUserId,
                        command.ActorDisplay,
                        user.Id,
                        command.Reason,
                        command.Confirmed,
                        command.CorrelationId),
                    user,
                    "system-batch",
                    cancellationToken);

                processedUsers++;
                changedCount += result.ChangedCount;
                failedCount += result.FailedCount;
            }

            lastProcessedUserId = users[^1].Id;
            _context.ChangeTracker.Clear();
        }

        string outcome = AdminAuditOutcome.Success;
        if (failedCount > 0)
        {
            outcome = AdminAuditOutcome.Failure;
        }

        await RecordSystemAuditAsync(
            command,
            outcome,
            processedUsers,
            changedCount,
            failedCount,
            "completed",
            cancellationToken);

        return AdminAchievementBatchSyncResult.FromCounts(
            processedUsers,
            changedCount,
            failedCount);
    }

    // Đếm số người đã nhận theo mã thành tích trong database.
    private async Task<Dictionary<string, int>> LoadRecipientCountsAsync(
        CancellationToken cancellationToken)
    {
        return await _context.UserAchievements
            .AsNoTracking()
            .GroupBy(achievement => achievement.Code)
            .Select(group => new CodeCount(group.Key, group.Count()))
            .ToDictionaryAsync(item => item.Code, item => item.Count, cancellationToken);
    }

    // Dùng danh mục trong mã nguồn làm dữ liệu gốc, không đọc định nghĩa có thể bị sửa từ database.
    private static IReadOnlyList<AdminAchievementDefinitionSummary> BuildCatalogSummaries(
        IReadOnlyDictionary<string, int> recipientCounts)
    {
        List<AdminAchievementDefinitionSummary> summaries = new();
        foreach (AchievementCatalog.Definition definition in AchievementCatalog.All)
        {
            int recipientCount = 0;
            if (recipientCounts.TryGetValue(definition.Code, out int storedCount))
            {
                recipientCount = storedCount;
            }

            summaries.Add(new AdminAchievementDefinitionSummary(
                definition.Code,
                definition.Title,
                definition.Description,
                definition.Metric.ToString(),
                definition.Target,
                recipientCount));
        }

        return summaries;
    }

    // Tạo kết quả theo user trên trang hiện tại, gồm số đã nhận, đủ điều kiện và còn thiếu.
    private async Task<IReadOnlyList<AdminAchievementUserResult>> BuildUserResultsAsync(
        IReadOnlyList<AppUser> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return [];
        }

        string[] userIds = users.Select(user => user.Id).ToArray();
        List<UserAchievement> achievements = await _context.UserAchievements
            .AsNoTracking()
            .Where(achievement => userIds.Contains(achievement.UserId))
            .ToListAsync(cancellationToken);
        Dictionary<string, List<UserAchievement>> achievementsByUser = achievements
            .GroupBy(achievement => achievement.UserId)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        IReadOnlyDictionary<string, AchievementProgressSnapshot> progressByUser =
            await _progressService.GetSnapshotsAsync(userIds, cancellationToken);

        List<AdminAchievementUserResult> results = new();
        foreach (AppUser user in users)
        {
            achievementsByUser.TryGetValue(user.Id, out List<UserAchievement>? userAchievements);
            userAchievements ??= [];
            AchievementProgressSnapshot snapshot = progressByUser[user.Id];
            HashSet<string> unlockedCodes = userAchievements
                .Select(achievement => achievement.Code)
                .ToHashSet(StringComparer.Ordinal);
            DateTime? lastUnlockedAtUtc = null;
            if (userAchievements.Count > 0)
            {
                lastUnlockedAtUtc = userAchievements.Max(item => item.UnlockedAt);
            }

            List<string> missingCodes = new();
            int eligibleCount = 0;
            foreach (AchievementCatalog.Definition definition in AchievementCatalog.All)
            {
                int metricValue = snapshot.GetValue(definition.Metric);
                if (metricValue < definition.Target)
                {
                    continue;
                }

                eligibleCount++;
                if (!unlockedCodes.Contains(definition.Code))
                {
                    missingCodes.Add(definition.Code);
                }
            }

            results.Add(new AdminAchievementUserResult(
                user.Id,
                user.UserName,
                user.Email,
                unlockedCodes.Count,
                eligibleCount,
                missingCodes.Count,
                lastUnlockedAtUtc,
                missingCodes));
        }

        return results;
    }

    // Lọc user theo email, tên đăng nhập hoặc mã định danh; giá trị rỗng trả về toàn bộ.
    private static IQueryable<AppUser> ApplySearch(
        IQueryable<AppUser> users,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return users;
        }

        string term = search.Trim();
        return users.Where(user =>
            user.Email.Contains(term)
            || user.UserName.Contains(term)
            || user.Id.Contains(term));
    }

    // Chạy đồng bộ một user trong transaction riêng và ghi audit cùng transaction khi thành công.
    private async Task<AdminAchievementSyncResult> RunUserSyncAsync(
        AdminAchievementSyncCommand command,
        AppUser target,
        string scope,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IDbContextTransaction transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);
            IReadOnlyList<AchievementCatalog.Definition> unlocked =
                await _unlockService.SyncEligibleAsync(command.TargetUserId, cancellationToken);

            _auditService.Enqueue(BuildUserAuditEntry(
                command,
                target,
                AdminAuditOutcome.Success,
                scope,
                unlocked.Count,
                null,
                null));
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return AdminAchievementSyncResult.Success(
                $"Đã đồng bộ thành tích cho {target.Email}. Thêm {unlocked.Count:N0} thành tích còn thiếu.",
                unlocked.Count);
        }
        catch (Exception exception)
        {
            // Xóa tracker sau lỗi để audit thất bại không vô tình lưu entity thành tích đang lỗi.
            _context.ChangeTracker.Clear();
            await RecordFailureAuditAsync(
                command,
                target,
                scope,
                exception.GetType().Name,
                cancellationToken);
            return AdminAchievementSyncResult.Failure("Đồng bộ thành tích thất bại. Hệ thống đã ghi audit để dashboard cảnh báo.");
        }
    }

    // Ghi audit bị từ chối khi có tác vụ khác đang chạy cùng phạm vi.
    private async Task RecordDeniedAuditAsync(
        AdminAchievementSyncCommand command,
        AppUser target,
        string denialReason,
        CancellationToken cancellationToken)
    {
        AdminAuditEntry entry = BuildUserAuditEntry(
            command,
            target,
            AdminAuditOutcome.Denied,
            "single-user",
            0,
            "duplicate",
            denialReason);
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Ghi audit thất bại ngoài transaction nghiệp vụ sau khi đã rollback đồng bộ user.
    private async Task RecordFailureAuditAsync(
        AdminAchievementSyncCommand command,
        AppUser target,
        string scope,
        string failureKind,
        CancellationToken cancellationToken)
    {
        AdminAuditEntry entry = BuildUserAuditEntry(
            command,
            target,
            AdminAuditOutcome.Failure,
            scope,
            0,
            "failed",
            failureKind);
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Ghi audit tổng hợp cho lệnh đồng bộ toàn hệ thống.
    private async Task RecordSystemAuditAsync(
        AdminAchievementBatchSyncCommand command,
        string outcome,
        int processedUsers,
        int changedCount,
        int failedCount,
        string status,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "system-batch",
            ["status"] = status,
            ["processedCount"] = processedUsers.ToString(),
            ["changedCount"] = changedCount.ToString(),
            ["failedCount"] = failedCount.ToString(),
            ["batchSize"] = NormalizeBatchSize(command.BatchSize).ToString()
        };

        var entry = new AdminAuditEntry(
            ActorUserId: command.ActorUserId,
            ActorDisplay: command.ActorDisplay,
            Action: AdminAuditActions.AchievementsResyncAll,
            Outcome: outcome,
            TargetType: "AchievementCatalog",
            TargetId: "system",
            Reason: command.Reason?.Trim(),
            CorrelationId: command.CorrelationId,
            Metadata: metadata);
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Tạo audit đồng bộ user chỉ gồm thông tin an toàn và các số đếm cần cho điều tra.
    private static AdminAuditEntry BuildUserAuditEntry(
        AdminAchievementSyncCommand command,
        AppUser target,
        string outcome,
        string scope,
        int changedCount,
        string? status,
        string? failureKind)
    {
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = scope,
            ["status"] = status ?? "completed",
            ["failureKind"] = failureKind,
            ["changedCount"] = changedCount.ToString(),
            ["count"] = changedCount.ToString()
        };

        return new AdminAuditEntry(
            ActorUserId: command.ActorUserId,
            ActorDisplay: command.ActorDisplay,
            Action: AdminAuditActions.AchievementsResyncUser,
            Outcome: outcome,
            TargetType: "AppUser",
            TargetId: target.Id,
            Reason: command.Reason?.Trim(),
            CorrelationId: command.CorrelationId,
            Metadata: metadata);
    }

    // Kiểm tra form đồng bộ user trước khi dùng database hoặc ghi audit.
    private static string? ValidateSyncCommand(AdminAchievementSyncCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        if (string.IsNullOrWhiteSpace(command.TargetUserId))
        {
            return "Vui lòng chọn người dùng cần đồng bộ.";
        }

        return ValidateReasonAndConfirmation(command.Reason, command.Confirmed);
    }

    // Kiểm tra form đồng bộ toàn hệ thống trước khi quét danh sách user.
    private static string? ValidateBatchCommand(AdminAchievementBatchSyncCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        return ValidateReasonAndConfirmation(command.Reason, command.Confirmed);
    }

    // Kiểm tra lý do và xác nhận, áp dụng chung cho mọi tác vụ ghi dữ liệu.
    private static string? ValidateReasonAndConfirmation(string? reason, bool confirmed)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Vui lòng nhập lý do trước khi đồng bộ thành tích.";
        }

        if (reason.Trim().Length > MaxReasonLength)
        {
            return "Ly do khong duoc vuot qua 500 ky tu.";
        }

        if (!confirmed)
        {
            return "Vui lòng xác nhận đây là thao tác đồng bộ lại từ dữ liệu học tập.";
        }

        return null;
    }

    // Chuẩn hóa kích thước lô để tránh form gửi giá trị quá lớn.
    private static int NormalizeBatchSize(int batchSize)
    {
        if (batchSize <= 0)
        {
            return DefaultBatchSize;
        }

        return Math.Clamp(batchSize, 1, MaxBatchSize);
    }

    private sealed record CodeCount(string Code, int Count);
}
