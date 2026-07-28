using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.ContentModeration;

// Nghiệp vụ cách ly/khôi phục bộ flashcard và bảo vệ nội dung riêng tư khi Admin mở chi tiết.
public sealed class ContentModerationService : IContentModerationService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    private const int MaxPublicReasonLength = 500;
    private const int MaxInternalTextLength = 1000;

    private readonly AppDbContext _context;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    // Nhận DbContext, audit service và đồng hồ để test có thể kiểm soát thời gian.
    public ContentModerationService(
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

    // Tìm kiếm danh sách bộ flashcard cho Admin, chỉ trả thông tin khái quát.
    public async Task<AdminContentSetPage> SearchSetsAsync(
        AdminContentSetQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Max` và lưu kết quả vào `page`.
        int page = Math.Max(DefaultPage, query.Page);
        // 2. Gọi `Clamp` và lưu kết quả vào `pageSize`.
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // 3. Gọi `AsNoTracking` và lưu kết quả vào `sets`.
        IQueryable<FlashcardSet> sets = _context.FlashcardSets.AsNoTracking();
        // 4. Cập nhật `sets` bằng giá trị mới.
        sets = ApplySearch(sets, query.Search);
        // 5. Cập nhật `sets` bằng giá trị mới.
        sets = ApplyStatusFilter(sets, query.Status);
        // 6. Cập nhật `sets` bằng giá trị mới.
        sets = ApplyVisibilityFilter(sets, query.Visibility);
        // 7. Cập nhật `sets` bằng giá trị mới.
        sets = sets.OrderByDescending(set => set.UpdatedAt).ThenByDescending(set => set.Id);

        // 8. Gọi `CountAsync` và lưu kết quả vào `totalCount`.
        int totalCount = await sets.CountAsync(cancellationToken);
        // 9. Gọi `ToListAsync` và lưu kết quả vào `rows`.
        List<AdminContentSetRow> rows = await sets
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(set => new AdminContentSetRow(
                set.Id,
                set.Title,
                _context.AppUsers
                    .Where(user => user.Id == set.UserId)
                    .Select(user => user.Email ?? user.UserName ?? user.Id)
                    .FirstOrDefault() ?? set.UserId,
                set.IsPublic,
                set.ModerationStatus,
                set.ModerationPublicReason,
                set.UpdatedAt,
                set.ModeratedAtUtc,
                set.Flashcards.Count,
                _context.ContentReports.Count(report =>
                    report.FlashcardSetId == set.Id
                    && report.Status == ContentReportStatus.Pending),
                set.ModerationVersion))
            .ToListAsync(cancellationToken);

        // 10. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminContentSetPage(rows, totalCount, page, pageSize);
    }

    // Mở chi tiết bộ flashcard; bộ riêng tư bắt buộc có lý do và được audit trước khi trả thẻ.
    public async Task<AdminContentSetDetailsResult> GetDetailsAsync(
        int flashcardSetId,
        AdminContentSetAccessCommand access,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == flashcardSetId, cancellationToken);
        // 2. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 3. Trả kết quả từ `NotFound` cho nơi gọi.
            return AdminContentSetDetailsResult.NotFound();
        }

        // 4. Kiểm tra `!set.IsPublic` để chọn nhánh xử lý phù hợp.
        if (!set.IsPublic)
        {
            // 5. Gọi `ValidateAccessReason` và lưu kết quả vào `reasonError`.
            string? reasonError = ValidateAccessReason(access);
            // 6. Kiểm tra `reasonError != null` để chọn nhánh xử lý phù hợp.
            if (reasonError != null)
            {
                // 7. Trả kết quả từ `ReasonRequired` cho nơi gọi.
                return AdminContentSetDetailsResult.ReasonRequired(reasonError);
            }

            // Audit được ghi trước khi lấy danh sách thẻ để không có lần xem nội dung riêng tư thiếu dấu vết.
            // 8. Gọi `RecordPrivateDetailsAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordPrivateDetailsAuditAsync(set, access, cancellationToken);
        }

        // 9. Gọi `BuildDetailsAsync` và lưu kết quả vào `details`.
        AdminContentSetDetails details = await BuildDetailsAsync(set, cancellationToken);
        // 10. Trả kết quả từ `Success` cho nơi gọi.
        return AdminContentSetDetailsResult.Success(details);
    }

    // Cách ly trực tiếp từ trang chi tiết hoặc danh sách nội dung.
    public async Task<ContentModerationOperationResult> QuarantineSetAsync(
        QuarantineFlashcardSetCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ValidateQuarantineCommand` và lưu kết quả vào `validationMessage`.
        string? validationMessage = ValidateQuarantineCommand(command);
        // 2. Kiểm tra `validationMessage != null` để chọn nhánh xử lý phù hợp.
        if (validationMessage != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(validationMessage);
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .SingleOrDefaultAsync(item => item.Id == command.FlashcardSetId, cancellationToken);
        // 5. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 6. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure("Không tìm thấy bộ flashcard cần cách ly.");
        }

        // 7. Gọi `DetectSetConflictOrClosedAsync` và lưu kết quả vào `denied`.
        ContentModerationOperationResult? denied =
            await DetectSetConflictOrClosedAsync(command, set, AdminAuditActions.ContentSetsQuarantine, cancellationToken);
        // 8. Kiểm tra `denied != null` để chọn nhánh xử lý phù hợp.
        if (denied != null)
        {
            // 9. Trả `denied` cho nơi gọi.
            return denied;
        }

        // 10. Trả kết quả từ `QuarantineSetInternalAsync` cho nơi gọi.
        return await QuarantineSetInternalAsync(
            set,
            command.PublicReason,
            command.InternalNote,
            command.Evidence,
            command.Actor.UserId,
            command.Actor.Display,
            command.Actor.CorrelationId,
            AdminAuditActions.ContentSetsQuarantine,
            null,
            cancellationToken);
    }

    // Cách ly từ một báo cáo đang chờ; báo cáo và các báo cáo đang chờ cùng bộ được đóng trong cùng transaction.
    public async Task<ContentModerationOperationResult> QuarantineFromReportAsync(
        QuarantineFromReportCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ValidateReportQuarantineCommand` và lưu kết quả vào `validationMessage`.
        string? validationMessage = ValidateReportQuarantineCommand(command);
        // 2. Kiểm tra `validationMessage != null` để chọn nhánh xử lý phù hợp.
        if (validationMessage != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(validationMessage);
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `report`.
        ContentReport? report = await _context.ContentReports
            .Include(item => item.FlashcardSet)
            .SingleOrDefaultAsync(item => item.Id == command.ReportId, cancellationToken);
        // 5. Kiểm tra `report == null || report.FlashcardSet == null` để chọn nhánh xử lý phù hợp.
        if (report == null || report.FlashcardSet == null)
        {
            // 6. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure("Không tìm thấy báo cáo cần xử lý.");
        }

        // 7. Gọi `DetectReportConflictOrClosedAsync` và lưu kết quả vào `denied`.
        ContentModerationOperationResult? denied =
            await DetectReportConflictOrClosedAsync(command, report, cancellationToken);
        // 8. Kiểm tra `denied != null` để chọn nhánh xử lý phù hợp.
        if (denied != null)
        {
            // 9. Trả `denied` cho nơi gọi.
            return denied;
        }

        // 10. Gọi `DetectSetConflictOrClosedAsync` và lưu kết quả vào `setDenied`.
        ContentModerationOperationResult? setDenied =
            await DetectSetConflictOrClosedAsync(command, report.FlashcardSet, AdminAuditActions.ContentReportsQuarantine, cancellationToken);
        // 11. Kiểm tra `setDenied != null` để chọn nhánh xử lý phù hợp.
        if (setDenied != null)
        {
            // 12. Trả `setDenied` cho nơi gọi.
            return setDenied;
        }

        // 13. Trả kết quả từ `QuarantineSetInternalAsync` cho nơi gọi.
        return await QuarantineSetInternalAsync(
            report.FlashcardSet,
            command.PublicReason,
            command.InternalNote,
            command.Evidence,
            command.Actor.UserId,
            command.Actor.Display,
            command.Actor.CorrelationId,
            AdminAuditActions.ContentReportsQuarantine,
            report.Id,
            cancellationToken);
    }

    // Khôi phục bộ đã cách ly; chỉ Admin gọi được qua controller trong Area Admin.
    public async Task<ContentModerationOperationResult> RestoreSetAsync(
        RestoreFlashcardSetCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ValidateRestoreCommand` và lưu kết quả vào `validationMessage`.
        string? validationMessage = ValidateRestoreCommand(command);
        // 2. Kiểm tra `validationMessage != null` để chọn nhánh xử lý phù hợp.
        if (validationMessage != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(validationMessage);
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .SingleOrDefaultAsync(item => item.Id == command.FlashcardSetId, cancellationToken);
        // 5. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 6. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure("Không tìm thấy bộ flashcard cần khôi phục.");
        }

        // 7. Kiểm tra `set.ModerationStatus != FlashcardSetModerationStatus.Quarantined` để chọn nhánh xử lý phù hợp.
        if (set.ModerationStatus != FlashcardSetModerationStatus.Quarantined)
        {
            // 8. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Bộ flashcard chưa bị cách ly nên không cần khôi phục.";
            // 9. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSetDeniedAuditAsync(command, AdminAuditActions.ContentSetsRestore, set, message, cancellationToken);
            // 10. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(message);
        }

        // 11. Kiểm tra `set.ModerationVersion != command.Version` để chọn nhánh xử lý phù hợp.
        if (set.ModerationVersion != command.Version)
        {
            // 12. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            // 13. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSetDeniedAuditAsync(command, AdminAuditActions.ContentSetsRestore, set, message, cancellationToken);
            // 14. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(message);
        }

        // 15. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // 16. Tính giá trị và lưu vào `nowUtc` để dùng ở bước tiếp theo.
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        // 17. Cập nhật `set.ModerationStatus` bằng giá trị mới.
        set.ModerationStatus = FlashcardSetModerationStatus.Active;
        // 18. Cập nhật `set.ModerationPublicReason` bằng giá trị mới.
        set.ModerationPublicReason = null;
        // 19. Cập nhật `set.ModerationInternalNote` bằng giá trị mới.
        set.ModerationInternalNote = null;
        // 20. Cập nhật `set.ModerationEvidence` bằng giá trị mới.
        set.ModerationEvidence = null;
        // 21. Cập nhật `set.ModeratedByUserId` bằng giá trị mới.
        set.ModeratedByUserId = command.Actor.UserId.Trim();
        // 22. Cập nhật `set.ModeratedAtUtc` bằng giá trị mới.
        set.ModeratedAtUtc = nowUtc;
        // 23. Cập nhật bộ đếm hoặc trạng thái `set.ModerationVersion`.
        set.ModerationVersion++;
        // 24. Cập nhật `set.UpdatedAt` bằng giá trị mới.
        set.UpdatedAt = nowUtc;

        // 25. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildSetAuditEntry(
            command.Actor.UserId,
            command.Actor.Display,
            AdminAuditActions.ContentSetsRestore,
            AdminAuditOutcome.Success,
            set,
            command.Reason,
            command.Actor.CorrelationId,
            null));

        // 26. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 27. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 28. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
            await transaction.CommitAsync(cancellationToken);
            // 29. Trả kết quả từ `Success` cho nơi gọi.
            return ContentModerationOperationResult.Success("Đã khôi phục bộ flashcard.");
        }
        catch (DbUpdateConcurrencyException)
        {
            // 30. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
            await transaction.RollbackAsync(cancellationToken);
            // 31. Gọi `DetachAllTrackedEntities` để thực hiện bước nghiệp vụ này.
            DetachAllTrackedEntities();
            // 32. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSetDeniedAuditAsync(command, AdminAuditActions.ContentSetsRestore, set, "Xung đột phiên bản.", cancellationToken);
            // 33. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure("Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.");
        }
    }

    // Cách ly bộ và đóng các báo cáo đang chờ trong cùng transaction.
    private async Task<ContentModerationOperationResult> QuarantineSetInternalAsync(
        FlashcardSet set,
        string publicReason,
        string? internalNote,
        string? evidence,
        string actorUserId,
        string actorDisplay,
        string? correlationId,
        string action,
        long? sourceReportId,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // 2. Tính giá trị và lưu vào `nowUtc` để dùng ở bước tiếp theo.
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        // 3. Gọi `Trim` và lưu kết quả vào `normalizedReason`.
        string normalizedReason = publicReason.Trim();

        // 4. Cập nhật `set.ModerationStatus` bằng giá trị mới.
        set.ModerationStatus = FlashcardSetModerationStatus.Quarantined;
        // 5. Cập nhật `set.ModerationPublicReason` bằng giá trị mới.
        set.ModerationPublicReason = normalizedReason;
        // 6. Cập nhật `set.ModerationInternalNote` bằng giá trị mới.
        set.ModerationInternalNote = NormalizeOptional(internalNote);
        // 7. Cập nhật `set.ModerationEvidence` bằng giá trị mới.
        set.ModerationEvidence = NormalizeOptional(evidence);
        // 8. Cập nhật `set.ModeratedByUserId` bằng giá trị mới.
        set.ModeratedByUserId = actorUserId.Trim();
        // 9. Cập nhật `set.ModeratedAtUtc` bằng giá trị mới.
        set.ModeratedAtUtc = nowUtc;
        // 10. Cập nhật bộ đếm hoặc trạng thái `set.ModerationVersion`.
        set.ModerationVersion++;
        // 11. Cập nhật `set.UpdatedAt` bằng giá trị mới.
        set.UpdatedAt = nowUtc;

        // 12. Gọi `ToListAsync` và lưu kết quả vào `pendingReports`.
        List<ContentReport> pendingReports = await _context.ContentReports
            .Where(report =>
                report.FlashcardSetId == set.Id
                && report.Status == ContentReportStatus.Pending)
            .ToListAsync(cancellationToken);

        // 13. Duyệt từng `report` trong `pendingReports` để xử lý lần lượt.
        foreach (ContentReport report in pendingReports)
        {
            // 14. Cập nhật `report.Status` bằng giá trị mới.
            report.Status = ContentReportStatus.Quarantined;
            // 15. Cập nhật `report.ResolutionOutcome` bằng giá trị mới.
            report.ResolutionOutcome = ContentReportResolutionOutcome.Quarantined;
            // 16. Cập nhật `report.ResolutionReason` bằng giá trị mới.
            report.ResolutionReason = normalizedReason;
            // 17. Cập nhật `report.ResolvedByUserId` bằng giá trị mới.
            report.ResolvedByUserId = actorUserId.Trim();
            // 18. Cập nhật `report.ResolvedAtUtc` bằng giá trị mới.
            report.ResolvedAtUtc = nowUtc;
            // 19. Cập nhật bộ đếm hoặc trạng thái `report.Version`.
            report.Version++;
        }

        // 20. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildSetAuditEntry(
            actorUserId,
            actorDisplay,
            action,
            AdminAuditOutcome.Success,
            set,
            normalizedReason,
            correlationId,
            sourceReportId));

        // 21. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 22. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 23. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
            await transaction.CommitAsync(cancellationToken);
            // 24. Trả kết quả từ `Success` cho nơi gọi.
            return ContentModerationOperationResult.Success("Đã cách ly bộ flashcard.");
        }
        catch (DbUpdateConcurrencyException)
        {
            // 25. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
            await transaction.RollbackAsync(cancellationToken);
            // 26. Gọi `DetachAllTrackedEntities` để thực hiện bước nghiệp vụ này.
            DetachAllTrackedEntities();
            // 27. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSetDeniedAuditAsync(actorUserId, actorDisplay, action, set, normalizedReason, correlationId, "Xung đột phiên bản.", cancellationToken);
            // 28. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure("Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.");
        }
    }

    // Dựng dữ liệu chi tiết sau khi đã qua cổng lý do nếu bộ riêng tư.
    private async Task<AdminContentSetDetails> BuildDetailsAsync(
        FlashcardSet set,
        CancellationToken cancellationToken)
    {
        // 1. Tính giá trị và lưu vào `ownerDisplay` để dùng ở bước tiếp theo.
        string ownerDisplay = await _context.AppUsers
            .AsNoTracking()
            .Where(user => user.Id == set.UserId)
            .Select(user => user.Email ?? user.UserName ?? user.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? set.UserId;

        // 2. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<AdminContentFlashcardRow> cards = await _context.Flashcards
            .AsNoTracking()
            .Where(card => card.FlashcardSetId == set.Id)
            .OrderBy(card => card.OrderIndex)
            .Select(card => new AdminContentFlashcardRow(
                card.Id,
                card.FrontText,
                card.BackText,
                card.PartOfSpeech,
                card.OrderIndex))
            .ToListAsync(cancellationToken);

        // 3. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminContentSetDetails(
            set.Id,
            set.Title,
            set.Description,
            ownerDisplay,
            set.IsPublic,
            set.ModerationStatus,
            set.ModerationPublicReason,
            set.ModerationInternalNote,
            set.ModerationEvidence,
            set.CreatedAt,
            set.UpdatedAt,
            set.ModeratedAtUtc,
            set.ModerationVersion,
            cards);
    }

    // Lọc theo từ khóa an toàn trên mã bộ, tiêu đề và tài khoản chủ sở hữu.
    private IQueryable<FlashcardSet> ApplySearch(IQueryable<FlashcardSet> sets, string? search)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(search)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(search))
        {
            // 2. Trả `sets` cho nơi gọi.
            return sets;
        }

        // 3. Gọi `Trim` và lưu kết quả vào `term`.
        string term = search.Trim();
        // 4. Tính giá trị và lưu vào `parsedSetId` để dùng ở bước tiếp theo.
        int? parsedSetId = null;
        // 5. Kiểm tra `int.TryParse(term, out int setId)` để chọn nhánh xử lý phù hợp.
        if (int.TryParse(term, out int setId))
        {
            // 6. Cập nhật `parsedSetId` bằng giá trị mới.
            parsedSetId = setId;
        }

        // 7. Trả kết quả từ `Where` cho nơi gọi.
        return sets.Where(set =>
            (parsedSetId != null && set.Id == parsedSetId.Value)
            || set.Title.Contains(term)
            || _context.AppUsers.Any(user =>
                user.Id == set.UserId
                && ((user.Email != null && user.Email.Contains(term))
                    || (user.UserName != null && user.UserName.Contains(term))
                    || user.Id.Contains(term))));
    }

    // Lọc theo trạng thái kiểm duyệt cố định.
    private static IQueryable<FlashcardSet> ApplyStatusFilter(
        IQueryable<FlashcardSet> sets,
        string? status)
    {
        // 1. Gọi `NormalizeToken` và lưu kết quả vào `normalizedStatus`.
        string normalizedStatus = NormalizeToken(status);
        // 2. Kiểm tra `normalizedStatus == "quarantined"` để chọn nhánh xử lý phù hợp.
        if (normalizedStatus == "quarantined")
        {
            // 3. Trả kết quả từ `Where` cho nơi gọi.
            return sets.Where(set => set.ModerationStatus == FlashcardSetModerationStatus.Quarantined);
        }

        // 4. Kiểm tra `normalizedStatus == "active"` để chọn nhánh xử lý phù hợp.
        if (normalizedStatus == "active")
        {
            // 5. Trả kết quả từ `Where` cho nơi gọi.
            return sets.Where(set => set.ModerationStatus == FlashcardSetModerationStatus.Active);
        }

        // 6. Trả `sets` cho nơi gọi.
        return sets;
    }

    // Lọc public/private cho Admin mà không mở nội dung thẻ.
    private static IQueryable<FlashcardSet> ApplyVisibilityFilter(
        IQueryable<FlashcardSet> sets,
        string? visibility)
    {
        // 1. Gọi `NormalizeToken` và lưu kết quả vào `normalizedVisibility`.
        string normalizedVisibility = NormalizeToken(visibility);
        // 2. Kiểm tra `normalizedVisibility == "public"` để chọn nhánh xử lý phù hợp.
        if (normalizedVisibility == "public")
        {
            // 3. Trả kết quả từ `Where` cho nơi gọi.
            return sets.Where(set => set.IsPublic);
        }

        // 4. Kiểm tra `normalizedVisibility == "private"` để chọn nhánh xử lý phù hợp.
        if (normalizedVisibility == "private")
        {
            // 5. Trả kết quả từ `Where` cho nơi gọi.
            return sets.Where(set => !set.IsPublic);
        }

        // 6. Trả `sets` cho nơi gọi.
        return sets;
    }

    // Phát hiện bộ đã cách ly hoặc form cũ trước khi ghi thay đổi.
    private async Task<ContentModerationOperationResult?> DetectSetConflictOrClosedAsync(
        QuarantineFlashcardSetCommand command,
        FlashcardSet set,
        string action,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `set.ModerationStatus == FlashcardSetModerationStatus.Quarantined` để chọn nhánh xử lý phù hợp.
        if (set.ModerationStatus == FlashcardSetModerationStatus.Quarantined)
        {
            // 2. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Bộ flashcard đã bị cách ly trước đó.";
            // 3. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSetDeniedAuditAsync(command, action, set, message, cancellationToken);
            // 4. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(message);
        }

        // 5. Kiểm tra `set.ModerationVersion != command.Version` để chọn nhánh xử lý phù hợp.
        if (set.ModerationVersion != command.Version)
        {
            // 6. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            // 7. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSetDeniedAuditAsync(command, action, set, message, cancellationToken);
            // 8. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(message);
        }

        // 9. Trả `null` cho nơi gọi.
        return null;
    }

    // Phát hiện bộ đã cách ly hoặc form cũ khi thao tác đi từ báo cáo.
    private async Task<ContentModerationOperationResult?> DetectSetConflictOrClosedAsync(
        QuarantineFromReportCommand command,
        FlashcardSet set,
        string action,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `set.ModerationStatus == FlashcardSetModerationStatus.Quarantined` để chọn nhánh xử lý phù hợp.
        if (set.ModerationStatus == FlashcardSetModerationStatus.Quarantined)
        {
            // 2. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Bộ flashcard đã bị cách ly trước đó.";
            // 3. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSetDeniedAuditAsync(command, action, set, message, cancellationToken);
            // 4. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(message);
        }

        // 5. Kiểm tra `set.ModerationVersion != command.FlashcardSetVersion` để chọn nhánh xử lý phù hợp.
        if (set.ModerationVersion != command.FlashcardSetVersion)
        {
            // 6. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            // 7. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordSetDeniedAuditAsync(command, action, set, message, cancellationToken);
            // 8. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(message);
        }

        // 9. Trả `null` cho nơi gọi.
        return null;
    }

    // Phát hiện báo cáo đã xử lý hoặc form báo cáo cũ trước khi cách ly từ hàng đợi.
    private async Task<ContentModerationOperationResult?> DetectReportConflictOrClosedAsync(
        QuarantineFromReportCommand command,
        ContentReport report,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `report.Status != ContentReportStatus.Pending` để chọn nhánh xử lý phù hợp.
        if (report.Status != ContentReportStatus.Pending)
        {
            // 2. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Báo cáo này đã được xử lý trước đó.";
            // 3. Gọi `RecordReportDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordReportDeniedAuditAsync(command, report, message, cancellationToken);
            // 4. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(message);
        }

        // 5. Kiểm tra `report.Version != command.ReportVersion` để chọn nhánh xử lý phù hợp.
        if (report.Version != command.ReportVersion)
        {
            // 6. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Báo cáo đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            // 7. Gọi `RecordReportDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordReportDeniedAuditAsync(command, report, message, cancellationToken);
            // 8. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentModerationOperationResult.Failure(message);
        }

        // 9. Trả `null` cho nơi gọi.
        return null;
    }

    // Ghi audit cho lần Admin xem chi tiết nội dung riêng tư.
    private async Task RecordPrivateDetailsAuditAsync(
        FlashcardSet set,
        AdminContentSetAccessCommand access,
        CancellationToken cancellationToken)
    {
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "private-flashcard-set",
            ["status"] = set.ModerationStatus
        };

        // 2. Khởi tạo `entry` với dữ liệu ban đầu cần thiết.
        var entry = new AdminAuditEntry(
            ActorUserId: access.Actor.UserId,
            ActorDisplay: access.Actor.Display,
            Action: AdminAuditActions.ContentSetsViewPrivateDetails,
            Outcome: AdminAuditOutcome.Success,
            TargetType: "FlashcardSet",
            TargetId: set.Id.ToString(),
            Reason: access.Reason!.Trim(),
            CorrelationId: access.Actor.CorrelationId,
            Metadata: metadata);

        // 3. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Ghi audit từ chối cho thao tác khôi phục.
    private async Task RecordSetDeniedAuditAsync(
        RestoreFlashcardSetCommand command,
        string action,
        FlashcardSet set,
        string denialReason,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
        await RecordSetDeniedAuditAsync(
            command.Actor.UserId,
            command.Actor.Display,
            action,
            set,
            command.Reason,
            command.Actor.CorrelationId,
            denialReason,
            cancellationToken);
    }

    // Ghi audit từ chối cho thao tác cách ly trực tiếp.
    private async Task RecordSetDeniedAuditAsync(
        QuarantineFlashcardSetCommand command,
        string action,
        FlashcardSet set,
        string denialReason,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
        await RecordSetDeniedAuditAsync(
            command.Actor.UserId,
            command.Actor.Display,
            action,
            set,
            command.PublicReason,
            command.Actor.CorrelationId,
            denialReason,
            cancellationToken);
    }

    // Ghi audit từ chối cho thao tác cách ly từ báo cáo nhưng lỗi nằm ở bộ flashcard.
    private async Task RecordSetDeniedAuditAsync(
        QuarantineFromReportCommand command,
        string action,
        FlashcardSet set,
        string denialReason,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `RecordSetDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
        await RecordSetDeniedAuditAsync(
            command.Actor.UserId,
            command.Actor.Display,
            action,
            set,
            command.PublicReason,
            command.Actor.CorrelationId,
            denialReason,
            cancellationToken);
    }

    // Ghi audit từ chối chung cho các thao tác nhắm vào bộ flashcard.
    private async Task RecordSetDeniedAuditAsync(
        string actorUserId,
        string actorDisplay,
        string action,
        FlashcardSet set,
        string reason,
        string? correlationId,
        string denialReason,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `BuildSetAuditEntry` và lưu kết quả vào `entry`.
        AdminAuditEntry entry = BuildSetAuditEntry(
            actorUserId,
            actorDisplay,
            action,
            AdminAuditOutcome.Denied,
            set,
            reason,
            correlationId,
            null,
            denialReason);

        // 2. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Ghi audit từ chối cho thao tác nhắm vào báo cáo.
    private async Task RecordReportDeniedAuditAsync(
        QuarantineFromReportCommand command,
        ContentReport report,
        string denialReason,
        CancellationToken cancellationToken)
    {
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "content-report",
            ["status"] = report.Status
        };

        // 2. Khởi tạo `entry` với dữ liệu ban đầu cần thiết.
        var entry = new AdminAuditEntry(
            ActorUserId: command.Actor.UserId,
            ActorDisplay: command.Actor.Display,
            Action: AdminAuditActions.ContentReportsQuarantine,
            Outcome: AdminAuditOutcome.Denied,
            TargetType: "ContentReport",
            TargetId: report.Id.ToString(),
            Reason: command.PublicReason,
            CorrelationId: command.Actor.CorrelationId,
            Metadata: metadata);

        // 3. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Dựng payload audit an toàn, không đưa ghi chú nội bộ hoặc bằng chứng vào metadata.
    private static AdminAuditEntry BuildSetAuditEntry(
        string actorUserId,
        string actorDisplay,
        string action,
        string outcome,
        FlashcardSet set,
        string reason,
        string? correlationId,
        long? sourceReportId,
        string? denialReason = null)
    {
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "flashcard-set-moderation",
            ["status"] = set.ModerationStatus
        };

        // 2. Kiểm tra `sourceReportId != null` để chọn nhánh xử lý phù hợp.
        if (sourceReportId != null)
        {
            // 3. Cập nhật `metadata["filter"]` bằng giá trị mới.
            metadata["filter"] = $"report:{sourceReportId.Value}";
        }

        // 4. Kiểm tra `denialReason != null` để chọn nhánh xử lý phù hợp.
        if (denialReason != null)
        {
            // 5. Cập nhật `metadata["deniedReason"]` bằng giá trị mới.
            metadata["deniedReason"] = denialReason;
        }

        // 6. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAuditEntry(
            ActorUserId: actorUserId,
            ActorDisplay: actorDisplay,
            Action: action,
            Outcome: outcome,
            TargetType: "FlashcardSet",
            TargetId: set.Id.ToString(),
            Reason: reason,
            CorrelationId: correlationId,
            Metadata: metadata);
    }

    // Kiểm tra lệnh cách ly trực tiếp trước khi đụng database.
    private static string? ValidateQuarantineCommand(QuarantineFlashcardSetCommand command)
    {
        // 1. Trả kết quả từ `ValidateQuarantineFields` cho nơi gọi.
        return ValidateQuarantineFields(
            command.Actor.UserId,
            command.PublicReason,
            command.InternalNote,
            command.Evidence,
            command.Version,
            command.Confirmed);
    }

    // Kiểm tra lệnh cách ly từ báo cáo trước khi đụng database.
    private static string? ValidateReportQuarantineCommand(QuarantineFromReportCommand command)
    {
        // 1. Kiểm tra `command.ReportVersion <= 0` để chọn nhánh xử lý phù hợp.
        if (command.ReportVersion <= 0)
        {
            // 2. Trả `"Thiếu mã phiên bản báo cáo. Vui lòng tải lại trang."` cho nơi gọi.
            return "Thiếu mã phiên bản báo cáo. Vui lòng tải lại trang.";
        }

        // 3. Trả kết quả từ `ValidateQuarantineFields` cho nơi gọi.
        return ValidateQuarantineFields(
            command.Actor.UserId,
            command.PublicReason,
            command.InternalNote,
            command.Evidence,
            command.FlashcardSetVersion,
            command.Confirmed);
    }

    // Kiểm tra các trường chung của thao tác cách ly.
    private static string? ValidateQuarantineFields(
        string actorUserId,
        string publicReason,
        string? internalNote,
        string? evidence,
        int version,
        bool confirmed)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(actorUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            // 2. Trả `"Không xác định được Quản trị viên đang thao tác."` cho nơi gọi.
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        // 3. Kiểm tra `!confirmed` để chọn nhánh xử lý phù hợp.
        if (!confirmed)
        {
            // 4. Trả `"Vui lòng xác nhận trước khi cách ly bộ flashcard."` cho nơi gọi.
            return "Vui lòng xác nhận trước khi cách ly bộ flashcard.";
        }

        // 5. Gọi `ValidateRequiredReason` và lưu kết quả vào `publicReasonError`.
        string? publicReasonError = ValidateRequiredReason(publicReason, "Lý do công khai");
        // 6. Kiểm tra `publicReasonError != null` để chọn nhánh xử lý phù hợp.
        if (publicReasonError != null)
        {
            // 7. Trả `publicReasonError` cho nơi gọi.
            return publicReasonError;
        }

        // 8. Gọi `ValidateOptionalLength` và lưu kết quả vào `internalNoteError`.
        string? internalNoteError = ValidateOptionalLength(internalNote, "Ghi chú nội bộ");
        // 9. Kiểm tra `internalNoteError != null` để chọn nhánh xử lý phù hợp.
        if (internalNoteError != null)
        {
            // 10. Trả `internalNoteError` cho nơi gọi.
            return internalNoteError;
        }

        // 11. Gọi `ValidateOptionalLength` và lưu kết quả vào `evidenceError`.
        string? evidenceError = ValidateOptionalLength(evidence, "Bằng chứng kiểm duyệt");
        // 12. Kiểm tra `evidenceError != null` để chọn nhánh xử lý phù hợp.
        if (evidenceError != null)
        {
            // 13. Trả `evidenceError` cho nơi gọi.
            return evidenceError;
        }

        // 14. Kiểm tra `version <= 0` để chọn nhánh xử lý phù hợp.
        if (version <= 0)
        {
            // 15. Trả `"Thiếu mã phiên bản bộ flashcard. Vui lòng tải lại trang."` cho nơi gọi.
            return "Thiếu mã phiên bản bộ flashcard. Vui lòng tải lại trang.";
        }

        // 16. Trả `null` cho nơi gọi.
        return null;
    }

    // Kiểm tra lệnh khôi phục trước khi ghi thay đổi.
    private static string? ValidateRestoreCommand(RestoreFlashcardSetCommand command)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(command.Actor.UserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.Actor.UserId))
        {
            // 2. Trả `"Không xác định được Quản trị viên đang thao tác."` cho nơi gọi.
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        // 3. Kiểm tra `!command.Confirmed` để chọn nhánh xử lý phù hợp.
        if (!command.Confirmed)
        {
            // 4. Trả `"Vui lòng xác nhận trước khi khôi phục bộ flashcard."` cho nơi gọi.
            return "Vui lòng xác nhận trước khi khôi phục bộ flashcard.";
        }

        // 5. Gọi `ValidateRequiredReason` và lưu kết quả vào `reasonError`.
        string? reasonError = ValidateRequiredReason(command.Reason, "Lý do khôi phục");
        // 6. Kiểm tra `reasonError != null` để chọn nhánh xử lý phù hợp.
        if (reasonError != null)
        {
            // 7. Trả `reasonError` cho nơi gọi.
            return reasonError;
        }

        // 8. Kiểm tra `command.Version <= 0` để chọn nhánh xử lý phù hợp.
        if (command.Version <= 0)
        {
            // 9. Trả `"Thiếu mã phiên bản bộ flashcard. Vui lòng tải lại trang."` cho nơi gọi.
            return "Thiếu mã phiên bản bộ flashcard. Vui lòng tải lại trang.";
        }

        // 10. Trả `null` cho nơi gọi.
        return null;
    }

    // Kiểm tra lý do mở nội dung riêng tư.
    private static string? ValidateAccessReason(AdminContentSetAccessCommand access)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(access.Actor.UserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(access.Actor.UserId))
        {
            // 2. Trả `"Không xác định được Quản trị viên đang xem."` cho nơi gọi.
            return "Không xác định được Quản trị viên đang xem.";
        }

        // 3. Kiểm tra `string.IsNullOrWhiteSpace(access.Reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(access.Reason))
        {
            // 4. Trả `"Vui lòng nhập lý do trước khi xem nội dung riêng tư."` cho nơi gọi.
            return "Vui lòng nhập lý do trước khi xem nội dung riêng tư.";
        }

        // 5. Kiểm tra `access.Reason.Trim().Length > MaxPublicReasonLength` để chọn nhánh xử lý phù hợp.
        if (access.Reason.Trim().Length > MaxPublicReasonLength)
        {
            // 6. Trả `"Lý do không được vượt quá 500 ký tự."` cho nơi gọi.
            return "Lý do không được vượt quá 500 ký tự.";
        }

        // 7. Trả `null` cho nơi gọi.
        return null;
    }

    // Kiểm tra lý do bắt buộc và giới hạn độ dài.
    private static string? ValidateRequiredReason(string value, string fieldName)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(value))
        {
            // 2. Trả `$"{fieldName} không được để trống."` cho nơi gọi.
            return $"{fieldName} không được để trống.";
        }

        // 3. Kiểm tra `value.Trim().Length > MaxPublicReasonLength` để chọn nhánh xử lý phù hợp.
        if (value.Trim().Length > MaxPublicReasonLength)
        {
            // 4. Trả `$"{fieldName} không được vượt quá 500 ký tự."` cho nơi gọi.
            return $"{fieldName} không được vượt quá 500 ký tự.";
        }

        // 5. Trả `null` cho nơi gọi.
        return null;
    }

    // Kiểm tra các ô nội bộ tùy chọn có vượt quá giới hạn lưu trữ hay không.
    private static string? ValidateOptionalLength(string? value, string fieldName)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(value))
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Kiểm tra `value.Trim().Length > MaxInternalTextLength` để chọn nhánh xử lý phù hợp.
        if (value.Trim().Length > MaxInternalTextLength)
        {
            // 4. Trả `$"{fieldName} không được vượt quá 1000 ký tự."` cho nơi gọi.
            return $"{fieldName} không được vượt quá 1000 ký tự.";
        }

        // 5. Trả `null` cho nơi gọi.
        return null;
    }

    // Chuẩn hóa token lọc từ query string.
    private static string NormalizeToken(string? value)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(value)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(value))
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 3. Trả kết quả từ `ToLowerInvariant` cho nơi gọi.
        return value.Trim().ToLowerInvariant();
    }

    // Cắt khoảng trắng và chuyển chuỗi rỗng thành null.
    private static string? NormalizeOptional(string? value)
    {
        // 1. Tính giá trị và lưu vào `trimmed` để dùng ở bước tiếp theo.
        string? trimmed = value?.Trim();
        // 2. Kiểm tra `string.IsNullOrWhiteSpace(trimmed)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Trả `trimmed` cho nơi gọi.
        return trimmed;
    }

    // Gỡ entity tracked sau concurrency exception để DbContext không giữ trạng thái lỗi.
    private void DetachAllTrackedEntities()
    {
        // 1. Duyệt từng `entry` trong `_context.ChangeTracker.Entries().ToList()` để xử lý lần lượt.
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry
                 in _context.ChangeTracker.Entries().ToList())
        {
            // 2. Cập nhật `entry.State` bằng giá trị mới.
            entry.State = EntityState.Detached;
        }
    }
}
