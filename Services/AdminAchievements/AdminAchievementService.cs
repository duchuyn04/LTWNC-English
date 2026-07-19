using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Achievements;
using ltwnc.Services.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.AdminAchievements;

// Service Admin cho thanh tich: chi doc du lieu va kich hoat tinh lai bang AchievementUnlockService hien co.
public sealed class AdminAchievementService : IAdminAchievementService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int DefaultBatchSize = 50;
    public const int MaxBatchSize = 200;
    private const int MaxReasonLength = 500;

    private readonly AppDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IAchievementProgressService _progressService;
    private readonly IAchievementUnlockService _unlockService;
    private readonly IAdminAuditService _auditService;
    private readonly AdminAchievementSyncCoordinator _syncCoordinator;

    // Nhan cac dependency doc/ghi can thiet; controller khong tu tinh thanh tich truc tiep.
    public AdminAchievementService(
        AppDbContext context,
        UserManager<IdentityUser> userManager,
        IAchievementProgressService progressService,
        IAchievementUnlockService unlockService,
        IAdminAuditService auditService,
        AdminAchievementSyncCoordinator syncCoordinator)
    {
        _context = context;
        _userManager = userManager;
        _progressService = progressService;
        _unlockService = unlockService;
        _auditService = auditService;
        _syncCoordinator = syncCoordinator;
    }

    // Lay catalog tu source code, dem so nguoi da nhan va tinh ket qua theo user tren trang hien tai.
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

        IQueryable<IdentityUser> users = ApplySearch(
            _context.Users.AsNoTracking(),
            query.Search);
        int totalUsers = await users.CountAsync(cancellationToken);
        List<IdentityUser> pageUsers = await users
            .OrderBy(user => user.Email)
            .ThenBy(user => user.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        IReadOnlyList<AdminAchievementUserResult> results =
            await BuildUserResultsAsync(pageUsers, cancellationToken);

        return new AdminAchievementOverview(catalog, results, totalUsers, page, pageSize);
    }

    // Dong bo mot user voi khoa chong chay trung, transaction va audit trong cung ket qua nghiep vu.
    public async Task<AdminAchievementSyncResult> ResyncUserAsync(
        AdminAchievementSyncCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationError = ValidateSyncCommand(command);
        if (validationError != null)
        {
            return AdminAchievementSyncResult.Failure(validationError, 0);
        }

        IdentityUser? target = await _userManager.FindByIdAsync(command.TargetUserId);
        if (target == null)
        {
            return AdminAchievementSyncResult.Failure("Khong tim thay nguoi dung can dong bo.");
        }

        using IDisposable? lease = _syncCoordinator.TryStartUser(command.TargetUserId);
        if (lease == null)
        {
            await RecordDeniedAuditAsync(
                command,
                target,
                "Tac vu dong bo thanh tich cho nguoi dung nay dang chay.",
                cancellationToken);
            return AdminAchievementSyncResult.Failure("Dang co tac vu dong bo thanh tich cho pham vi nay. Vui long thu lai sau.");
        }

        return await RunUserSyncAsync(
            command,
            target,
            "single-user",
            cancellationToken);
    }

    // Dong bo toan he thong theo cac lo nho; moi user co transaction rieng de loi khong de lai nua trang thai.
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
            return AdminAchievementBatchSyncResult.Failure("Dang co tac vu dong bo thanh tich khac dang chay. Vui long thu lai sau.");
        }

        List<string> userIds = await _context.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        int processedUsers = 0;
        int changedCount = 0;
        int failedCount = 0;
        foreach (string[] chunk in userIds.Chunk(NormalizeBatchSize(command.BatchSize)))
        {
            // Xu ly theo lo de request lon khong giu qua nhieu entity trong mot DbContext.
            foreach (string userId in chunk)
            {
                IdentityUser? user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    failedCount++;
                    continue;
                }

                AdminAchievementSyncResult result = await RunUserSyncAsync(
                    new AdminAchievementSyncCommand(
                        command.ActorUserId,
                        command.ActorDisplay,
                        userId,
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

    // Dem so nguoi da nhan theo ma thanh tich trong database.
    private async Task<Dictionary<string, int>> LoadRecipientCountsAsync(
        CancellationToken cancellationToken)
    {
        return await _context.UserAchievements
            .AsNoTracking()
            .GroupBy(achievement => achievement.Code)
            .Select(group => new CodeCount(group.Key, group.Count()))
            .ToDictionaryAsync(item => item.Code, item => item.Count, cancellationToken);
    }

    // Dung catalog trong source code lam danh muc goc, khong doc dinh nghia co the bi sua tu database.
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

    // Tao ket qua theo user tren trang hien tai, gom so da nhan, du dieu kien va con thieu.
    private async Task<IReadOnlyList<AdminAchievementUserResult>> BuildUserResultsAsync(
        IReadOnlyList<IdentityUser> users,
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

        List<AdminAchievementUserResult> results = new();
        foreach (IdentityUser user in users)
        {
            achievementsByUser.TryGetValue(user.Id, out List<UserAchievement>? userAchievements);
            userAchievements ??= [];
            AchievementProgressSnapshot snapshot =
                await _progressService.GetSnapshotAsync(user.Id, cancellationToken);
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
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                unlockedCodes.Count,
                eligibleCount,
                missingCodes.Count,
                lastUnlockedAtUtc,
                missingCodes));
        }

        return results;
    }

    // Loc user theo email, username hoac id; gia tri rong tra ve toan bo.
    private static IQueryable<IdentityUser> ApplySearch(
        IQueryable<IdentityUser> users,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return users;
        }

        string term = search.Trim();
        return users.Where(user =>
            (user.Email != null && user.Email.Contains(term))
            || (user.UserName != null && user.UserName.Contains(term))
            || user.Id.Contains(term));
    }

    // Chay sync cho mot user trong transaction rieng va ghi audit cung transaction khi thanh cong.
    private async Task<AdminAchievementSyncResult> RunUserSyncAsync(
        AdminAchievementSyncCommand command,
        IdentityUser target,
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
                $"Da dong bo thanh tich cho {target.Email ?? target.UserName}. Them {unlocked.Count:N0} thanh tich con thieu.",
                unlocked.Count);
        }
        catch (Exception exception)
        {
            // Xoa tracker sau loi de audit that bai khong vo tinh luu lai entity thanh tich dang bi loi.
            _context.ChangeTracker.Clear();
            await RecordFailureAuditAsync(
                command,
                target,
                scope,
                exception.GetType().Name,
                cancellationToken);
            return AdminAchievementSyncResult.Failure("Dong bo thanh tich that bai. He thong da ghi audit de dashboard canh bao.");
        }
    }

    // Ghi audit bi tu choi khi chay trung scope.
    private async Task RecordDeniedAuditAsync(
        AdminAchievementSyncCommand command,
        IdentityUser target,
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

    // Ghi audit that bai ngoai transaction nghiep vu sau khi da rollback user sync.
    private async Task RecordFailureAuditAsync(
        AdminAchievementSyncCommand command,
        IdentityUser target,
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

    // Ghi audit tong hop cho lenh dong bo toan he thong.
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

    // Tao audit user sync chi gom thong tin an toan va cac count can cho dieu tra.
    private static AdminAuditEntry BuildUserAuditEntry(
        AdminAchievementSyncCommand command,
        IdentityUser target,
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
            TargetType: "IdentityUser",
            TargetId: target.Id,
            Reason: command.Reason?.Trim(),
            CorrelationId: command.CorrelationId,
            Metadata: metadata);
    }

    // Kiem tra form dong bo user truoc khi dung database hoac ghi audit.
    private static string? ValidateSyncCommand(AdminAchievementSyncCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            return "Khong xac dinh duoc Quan tri vien dang thao tac.";
        }

        if (string.IsNullOrWhiteSpace(command.TargetUserId))
        {
            return "Vui long chon nguoi dung can dong bo.";
        }

        return ValidateReasonAndConfirmation(command.Reason, command.Confirmed);
    }

    // Kiem tra form dong bo toan he thong truoc khi quet danh sach user.
    private static string? ValidateBatchCommand(AdminAchievementBatchSyncCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            return "Khong xac dinh duoc Quan tri vien dang thao tac.";
        }

        return ValidateReasonAndConfirmation(command.Reason, command.Confirmed);
    }

    // Kiem tra ly do va checkbox xac nhan, ap dung chung cho moi tac vu ghi du lieu.
    private static string? ValidateReasonAndConfirmation(string? reason, bool confirmed)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Vui long nhap ly do truoc khi dong bo thanh tich.";
        }

        if (reason.Trim().Length > MaxReasonLength)
        {
            return "Ly do khong duoc vuot qua 500 ky tu.";
        }

        if (!confirmed)
        {
            return "Vui long xac nhan day la thao tac dong bo lai tu du lieu hoc tap.";
        }

        return null;
    }

    // Chuan hoa kich thuoc lo de tranh form gui gia tri qua lon.
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
