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
    // Giới hạn phân trang, kích thước lô và độ dài lý do để bảo vệ truy vấn quản trị.
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int DefaultBatchSize = 50;
    public const int MaxBatchSize = 200;
    private const int MaxReasonLength = 500;

    // Các dependency đọc dữ liệu, tính tiến độ, mở khóa, ghi audit và chống chạy trùng.
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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_progressService` để các phương thức khác sử dụng.
        _progressService = progressService;
        // 3. Lưu dependency `_unlockService` để các phương thức khác sử dụng.
        _unlockService = unlockService;
        // 4. Lưu dependency `_auditService` để các phương thức khác sử dụng.
        _auditService = auditService;
        // 5. Lưu dependency `_syncCoordinator` để các phương thức khác sử dụng.
        _syncCoordinator = syncCoordinator;
    }

    // Lấy danh mục từ mã nguồn, đếm người đã nhận và tính kết quả cho user trên trang hiện tại.
    public async Task<AdminAchievementOverview> GetOverviewAsync(
        AdminAchievementQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Max` và lưu kết quả vào `page`.
        int page = Math.Max(DefaultPage, query.Page);
        // 2. Gọi `Clamp` và lưu kết quả vào `pageSize`.
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // 3. Gọi `LoadRecipientCountsAsync` và lưu kết quả vào `recipientCounts`.
        Dictionary<string, int> recipientCounts =
            await LoadRecipientCountsAsync(cancellationToken);
        // 4. Gọi `BuildCatalogSummaries` và lưu kết quả vào `catalog`.
        IReadOnlyList<AdminAchievementDefinitionSummary> catalog =
            BuildCatalogSummaries(recipientCounts);

        // 5. Gọi `ApplySearch` và lưu kết quả vào `users`.
        IQueryable<AppUser> users = ApplySearch(
            _context.AppUsers.AsNoTracking(),
            query.Search);
        // 6. Gọi `CountAsync` và lưu kết quả vào `totalUsers`.
        int totalUsers = await users.CountAsync(cancellationToken);
        // 7. Gọi `ToListAsync` và lưu kết quả vào `pageUsers`.
        List<AppUser> pageUsers = await users
            .OrderBy(user => user.Email)
            .ThenBy(user => user.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 8. Gọi `BuildUserResultsAsync` và lưu kết quả vào `results`.
        IReadOnlyList<AdminAchievementUserResult> results =
            await BuildUserResultsAsync(pageUsers, cancellationToken);

        // 9. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAchievementOverview(catalog, results, totalUsers, page, pageSize);
    }

    // Đồng bộ một user với khóa chống chạy trùng, transaction và audit trong cùng kết quả nghiệp vụ.
    public async Task<AdminAchievementSyncResult> ResyncUserAsync(
        AdminAchievementSyncCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ValidateSyncCommand` và lưu kết quả vào `validationError`.
        string? validationError = ValidateSyncCommand(command);
        // 2. Kiểm tra `validationError != null` để chọn nhánh xử lý phù hợp.
        if (validationError != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminAchievementSyncResult.Failure(validationError, 0);
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `target`.
        AppUser? target = await _context.AppUsers.SingleOrDefaultAsync(
            item => item.Id == command.TargetUserId,
            cancellationToken);
        // 5. Kiểm tra `target == null` để chọn nhánh xử lý phù hợp.
        if (target == null)
        {
            // 6. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminAchievementSyncResult.Failure("Không tìm thấy người dùng cần đồng bộ.");
        }

        // 7. Gọi `TryStartUser` và lưu kết quả vào `lease`.
        using IDisposable? lease = _syncCoordinator.TryStartUser(command.TargetUserId);
        // 8. Kiểm tra `lease == null` để chọn nhánh xử lý phù hợp.
        if (lease == null)
        {
            // 9. Gọi `RecordDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordDeniedAuditAsync(
                command,
                target,
                "Tác vụ đồng bộ thành tích cho người dùng này đang chạy.",
                cancellationToken);
            // 10. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminAchievementSyncResult.Failure("Đang có tác vụ đồng bộ thành tích cho phạm vi này. Vui lòng thử lại sau.");
        }

        // 11. Trả kết quả từ `RunUserSyncAsync` cho nơi gọi.
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
        // 1. Gọi `ValidateBatchCommand` và lưu kết quả vào `validationError`.
        string? validationError = ValidateBatchCommand(command);
        // 2. Kiểm tra `validationError != null` để chọn nhánh xử lý phù hợp.
        if (validationError != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminAchievementBatchSyncResult.Failure(validationError);
        }

        // 4. Gọi `TryStartSystem` và lưu kết quả vào `lease`.
        using IDisposable? lease = _syncCoordinator.TryStartSystem();
        // 5. Kiểm tra `lease == null` để chọn nhánh xử lý phù hợp.
        if (lease == null)
        {
            // 6. Gọi `RecordSystemAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSystemAuditAsync(
                command,
                AdminAuditOutcome.Denied,
                0,
                0,
                1,
                "duplicate",
                cancellationToken);
            // 7. Trả kết quả từ `Failure` cho nơi gọi.
            return AdminAchievementBatchSyncResult.Failure("Đang có tác vụ đồng bộ thành tích khác chạy. Vui lòng thử lại sau.");
        }

        // 8. Tính giá trị và lưu vào `processedUsers` để dùng ở bước tiếp theo.
        int processedUsers = 0;
        // 9. Tính giá trị và lưu vào `changedCount` để dùng ở bước tiếp theo.
        int changedCount = 0;
        // 10. Tính giá trị và lưu vào `failedCount` để dùng ở bước tiếp theo.
        int failedCount = 0;
        // 11. Gọi `NormalizeBatchSize` và lưu kết quả vào `batchSize`.
        int batchSize = NormalizeBatchSize(command.BatchSize);
        // 12. Tính giá trị và lưu vào `lastProcessedUserId` để dùng ở bước tiếp theo.
        string? lastProcessedUserId = null;
        // 13. Tiếp tục lặp khi `true` còn đúng.
        while (true)
        {
            // 14. Gọi `AsNoTracking` và lưu kết quả vào `userBatchQuery`.
            IQueryable<AppUser> userBatchQuery = _context.AppUsers.AsNoTracking();
            // 15. Kiểm tra `lastProcessedUserId != null` để chọn nhánh xử lý phù hợp.
            if (lastProcessedUserId != null)
            {
                // Keyset theo Id giữ vị trí ổn định khi có tài khoản mới xuất hiện giữa hai batch.
                // 16. Cập nhật `userBatchQuery` bằng giá trị mới.
                userBatchQuery = userBatchQuery.Where(user =>
                    string.Compare(user.Id, lastProcessedUserId) > 0);
            }

            // Chỉ đọc một trang user từ database để không giữ toàn bộ mã tài khoản trong bộ nhớ.
            // 17. Gọi `ToListAsync` và lưu kết quả vào `users`.
            List<AppUser> users = await userBatchQuery
                .OrderBy(user => user.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            // 18. Kiểm tra `users.Count == 0` để chọn nhánh xử lý phù hợp.
            if (users.Count == 0)
            {
                // 19. Thoát khỏi vòng lặp hoặc nhánh xử lý hiện tại.
                break;
            }

            // 20. Duyệt từng `user` trong `users` để xử lý lần lượt.
            foreach (AppUser user in users)
            {
                // 21. Gọi `RunUserSyncAsync` và lưu kết quả vào `result`.
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

                // 22. Cập nhật bộ đếm hoặc trạng thái `processedUsers`.
                processedUsers++;
                // 23. Cập nhật `changedCount` bằng giá trị mới.
                changedCount += result.ChangedCount;
                // 24. Cập nhật `failedCount` bằng giá trị mới.
                failedCount += result.FailedCount;
            }

            // 25. Cập nhật `lastProcessedUserId` bằng giá trị mới.
            lastProcessedUserId = users[^1].Id;
            // 26. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
        }

        // 27. Tính giá trị và lưu vào `outcome` để dùng ở bước tiếp theo.
        string outcome = AdminAuditOutcome.Success;
        // 28. Kiểm tra `failedCount > 0` để chọn nhánh xử lý phù hợp.
        if (failedCount > 0)
        {
            // 29. Cập nhật `outcome` bằng giá trị mới.
            outcome = AdminAuditOutcome.Failure;
        }

        // 30. Gọi `RecordSystemAuditAsync` để thực hiện bước nghiệp vụ này.
        await RecordSystemAuditAsync(
            command,
            outcome,
            processedUsers,
            changedCount,
            failedCount,
            "completed",
            cancellationToken);

        // 31. Trả kết quả từ `FromCounts` cho nơi gọi.
        return AdminAchievementBatchSyncResult.FromCounts(
            processedUsers,
            changedCount,
            failedCount);
    }

    // Đếm số người đã nhận theo mã thành tích trong database.
    private async Task<Dictionary<string, int>> LoadRecipientCountsAsync(
        CancellationToken cancellationToken)
    {
        // 1. Trả kết quả từ `ToDictionaryAsync` cho nơi gọi.
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
        // 1. Khởi tạo `summaries` với dữ liệu ban đầu cần thiết.
        List<AdminAchievementDefinitionSummary> summaries = new();
        // 2. Duyệt từng `definition` trong `AchievementCatalog.All` để xử lý lần lượt.
        foreach (AchievementCatalog.Definition definition in AchievementCatalog.All)
        {
            // 3. Tính giá trị và lưu vào `recipientCount` để dùng ở bước tiếp theo.
            int recipientCount = 0;
            // 4. Kiểm tra `recipientCounts.TryGetValue(definition.Code, out int storedCount)` để chọn nhánh xử lý phù hợp.
            if (recipientCounts.TryGetValue(definition.Code, out int storedCount))
            {
                // 5. Cập nhật `recipientCount` bằng giá trị mới.
                recipientCount = storedCount;
            }

            // 6. Gọi `Add` để thực hiện bước nghiệp vụ này.
            summaries.Add(new AdminAchievementDefinitionSummary(
                definition.Code,
                definition.Title,
                definition.Description,
                definition.Metric.ToString(),
                definition.Target,
                recipientCount));
        }

        // 7. Trả `summaries` cho nơi gọi.
        return summaries;
    }

    // Tạo kết quả theo user trên trang hiện tại, gồm số đã nhận, đủ điều kiện và còn thiếu.
    private async Task<IReadOnlyList<AdminAchievementUserResult>> BuildUserResultsAsync(
        IReadOnlyList<AppUser> users,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `users.Count == 0` để chọn nhánh xử lý phù hợp.
        if (users.Count == 0)
        {
            // 2. Trả `[]` cho nơi gọi.
            return [];
        }

        // 3. Gọi `ToArray` và lưu kết quả vào `userIds`.
        string[] userIds = users.Select(user => user.Id).ToArray();
        // 4. Gọi `ToListAsync` và lưu kết quả vào `achievements`.
        List<UserAchievement> achievements = await _context.UserAchievements
            .AsNoTracking()
            .Where(achievement => userIds.Contains(achievement.UserId))
            .ToListAsync(cancellationToken);
        // 5. Gọi `ToDictionary` và lưu kết quả vào `achievementsByUser`.
        Dictionary<string, List<UserAchievement>> achievementsByUser = achievements
            .GroupBy(achievement => achievement.UserId)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        // 6. Gọi `GetSnapshotsAsync` và lưu kết quả vào `progressByUser`.
        IReadOnlyDictionary<string, AchievementProgressSnapshot> progressByUser =
            await _progressService.GetSnapshotsAsync(userIds, cancellationToken);

        // 7. Khởi tạo `results` với dữ liệu ban đầu cần thiết.
        List<AdminAchievementUserResult> results = new();
        // 8. Duyệt từng `user` trong `users` để xử lý lần lượt.
        foreach (AppUser user in users)
        {
            // 9. Gọi `TryGetValue` để thực hiện bước nghiệp vụ này.
            achievementsByUser.TryGetValue(user.Id, out List<UserAchievement>? userAchievements);
            // 10. Cập nhật `userAchievements` bằng giá trị mới.
            userAchievements ??= [];
            // 11. Tính giá trị và lưu vào `snapshot` để dùng ở bước tiếp theo.
            AchievementProgressSnapshot snapshot = progressByUser[user.Id];
            // 12. Gọi `ToHashSet` và lưu kết quả vào `unlockedCodes`.
            HashSet<string> unlockedCodes = userAchievements
                .Select(achievement => achievement.Code)
                .ToHashSet(StringComparer.Ordinal);
            // 13. Tính giá trị và lưu vào `lastUnlockedAtUtc` để dùng ở bước tiếp theo.
            DateTime? lastUnlockedAtUtc = null;
            // 14. Kiểm tra `userAchievements.Count > 0` để chọn nhánh xử lý phù hợp.
            if (userAchievements.Count > 0)
            {
                // 15. Cập nhật `lastUnlockedAtUtc` bằng giá trị mới.
                lastUnlockedAtUtc = userAchievements.Max(item => item.UnlockedAt);
            }

            // 16. Khởi tạo `missingCodes` với dữ liệu ban đầu cần thiết.
            List<string> missingCodes = new();
            // 17. Tính giá trị và lưu vào `eligibleCount` để dùng ở bước tiếp theo.
            int eligibleCount = 0;
            // 18. Duyệt từng `definition` trong `AchievementCatalog.All` để xử lý lần lượt.
            foreach (AchievementCatalog.Definition definition in AchievementCatalog.All)
            {
                // 19. Gọi `GetValue` và lưu kết quả vào `metricValue`.
                int metricValue = snapshot.GetValue(definition.Metric);
                // 20. Kiểm tra `metricValue < definition.Target` để chọn nhánh xử lý phù hợp.
                if (metricValue < definition.Target)
                {
                    // 21. Bỏ qua phần còn lại và chuyển sang lần lặp tiếp theo.
                    continue;
                }

                // 22. Cập nhật bộ đếm hoặc trạng thái `eligibleCount`.
                eligibleCount++;
                // 23. Kiểm tra `!unlockedCodes.Contains(definition.Code)` để chọn nhánh xử lý phù hợp.
                if (!unlockedCodes.Contains(definition.Code))
                {
                    // 24. Gọi `Add` để thực hiện bước nghiệp vụ này.
                    missingCodes.Add(definition.Code);
                }
            }

            // 25. Gọi `Add` để thực hiện bước nghiệp vụ này.
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

        // 26. Trả `results` cho nơi gọi.
        return results;
    }

    // Lọc user theo email, tên đăng nhập hoặc mã định danh; giá trị rỗng trả về toàn bộ.
    private static IQueryable<AppUser> ApplySearch(
        IQueryable<AppUser> users,
        string? search)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(search)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(search))
        {
            // 2. Trả `users` cho nơi gọi.
            return users;
        }

        // 3. Gọi `Trim` và lưu kết quả vào `term`.
        string term = search.Trim();
        // 4. Trả kết quả từ `Where` cho nơi gọi.
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
        // 1. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 2. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
            await using IDbContextTransaction transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);
            // 3. Gọi `SyncEligibleAsync` và lưu kết quả vào `unlocked`.
            IReadOnlyList<AchievementCatalog.Definition> unlocked =
                await _unlockService.SyncEligibleAsync(command.TargetUserId, cancellationToken);

            // 4. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
            _auditService.Enqueue(BuildUserAuditEntry(
                command,
                target,
                AdminAuditOutcome.Success,
                scope,
                unlocked.Count,
                null,
                null));
            // 5. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 6. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
            await transaction.CommitAsync(cancellationToken);

            // 7. Trả kết quả từ `Success` cho nơi gọi.
            return AdminAchievementSyncResult.Success(
                $"Đã đồng bộ thành tích cho {target.Email}. Thêm {unlocked.Count:N0} thành tích còn thiếu.",
                unlocked.Count);
        }
        catch (Exception exception)
        {
            // Xóa tracker sau lỗi để audit thất bại không vô tình lưu entity thành tích đang lỗi.
            // 8. Gọi `Clear` để thực hiện bước nghiệp vụ này.
            _context.ChangeTracker.Clear();
            // 9. Gọi `RecordFailureAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordFailureAuditAsync(
                command,
                target,
                scope,
                exception.GetType().Name,
                cancellationToken);
            // 10. Trả kết quả từ `Failure` cho nơi gọi.
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
        // 1. Gọi `BuildUserAuditEntry` và lưu kết quả vào `entry`.
        AdminAuditEntry entry = BuildUserAuditEntry(
            command,
            target,
            AdminAuditOutcome.Denied,
            "single-user",
            0,
            "duplicate",
            denialReason);
        // 2. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
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
        // 1. Gọi `BuildUserAuditEntry` và lưu kết quả vào `entry`.
        AdminAuditEntry entry = BuildUserAuditEntry(
            command,
            target,
            AdminAuditOutcome.Failure,
            scope,
            0,
            "failed",
            failureKind);
        // 2. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
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
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "system-batch",
            ["status"] = status,
            ["processedCount"] = processedUsers.ToString(),
            ["changedCount"] = changedCount.ToString(),
            ["failedCount"] = failedCount.ToString(),
            ["batchSize"] = NormalizeBatchSize(command.BatchSize).ToString()
        };

        // 2. Khởi tạo `entry` với dữ liệu ban đầu cần thiết.
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
        // 3. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
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
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = scope,
            ["status"] = status ?? "completed",
            ["failureKind"] = failureKind,
            ["changedCount"] = changedCount.ToString(),
            ["count"] = changedCount.ToString()
        };

        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
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
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(command.ActorUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            // 2. Trả `"Không xác định được Quản trị viên đang thao tác."` cho nơi gọi.
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        // 3. Kiểm tra `string.IsNullOrWhiteSpace(command.TargetUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.TargetUserId))
        {
            // 4. Trả `"Vui lòng chọn người dùng cần đồng bộ."` cho nơi gọi.
            return "Vui lòng chọn người dùng cần đồng bộ.";
        }

        // 5. Trả kết quả từ `ValidateReasonAndConfirmation` cho nơi gọi.
        return ValidateReasonAndConfirmation(command.Reason, command.Confirmed);
    }

    // Kiểm tra form đồng bộ toàn hệ thống trước khi quét danh sách user.
    private static string? ValidateBatchCommand(AdminAchievementBatchSyncCommand command)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(command.ActorUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            // 2. Trả `"Không xác định được Quản trị viên đang thao tác."` cho nơi gọi.
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        // 3. Trả kết quả từ `ValidateReasonAndConfirmation` cho nơi gọi.
        return ValidateReasonAndConfirmation(command.Reason, command.Confirmed);
    }

    // Kiểm tra lý do và xác nhận, áp dụng chung cho mọi tác vụ ghi dữ liệu.
    private static string? ValidateReasonAndConfirmation(string? reason, bool confirmed)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(reason))
        {
            // 2. Trả `"Vui lòng nhập lý do trước khi đồng bộ thành tích."` cho nơi gọi.
            return "Vui lòng nhập lý do trước khi đồng bộ thành tích.";
        }

        // 3. Kiểm tra `reason.Trim().Length > MaxReasonLength` để chọn nhánh xử lý phù hợp.
        if (reason.Trim().Length > MaxReasonLength)
        {
            // 4. Trả `"Ly do khong duoc vuot qua 500 ky tu."` cho nơi gọi.
            return "Ly do khong duoc vuot qua 500 ky tu.";
        }

        // 5. Kiểm tra `!confirmed` để chọn nhánh xử lý phù hợp.
        if (!confirmed)
        {
            // 6. Trả `"Vui lòng xác nhận đây là thao tác đồng bộ lại từ dữ liệu học tập."` cho nơi gọi.
            return "Vui lòng xác nhận đây là thao tác đồng bộ lại từ dữ liệu học tập.";
        }

        // 7. Trả `null` cho nơi gọi.
        return null;
    }

    // Chuẩn hóa kích thước lô để tránh form gửi giá trị quá lớn.
    private static int NormalizeBatchSize(int batchSize)
    {
        // 1. Kiểm tra `batchSize <= 0` để chọn nhánh xử lý phù hợp.
        if (batchSize <= 0)
        {
            // 2. Trả `DefaultBatchSize` cho nơi gọi.
            return DefaultBatchSize;
        }

        // 3. Trả kết quả từ `Clamp` cho nơi gọi.
        return Math.Clamp(batchSize, 1, MaxBatchSize);
    }

    private sealed record CodeCount(string Code, int Count);
}
