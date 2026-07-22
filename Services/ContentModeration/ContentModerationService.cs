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
        _context = context;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    // Tìm kiếm danh sách bộ flashcard cho Admin, chỉ trả thông tin khái quát.
    public async Task<AdminContentSetPage> SearchSetsAsync(
        AdminContentSetQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(DefaultPage, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        IQueryable<FlashcardSet> sets = _context.FlashcardSets.AsNoTracking();
        sets = ApplySearch(sets, query.Search);
        sets = ApplyStatusFilter(sets, query.Status);
        sets = ApplyVisibilityFilter(sets, query.Visibility);
        sets = sets.OrderByDescending(set => set.UpdatedAt).ThenByDescending(set => set.Id);

        int totalCount = await sets.CountAsync(cancellationToken);
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

        return new AdminContentSetPage(rows, totalCount, page, pageSize);
    }

    // Mở chi tiết bộ flashcard; bộ riêng tư bắt buộc có lý do và được audit trước khi trả thẻ.
    public async Task<AdminContentSetDetailsResult> GetDetailsAsync(
        int flashcardSetId,
        AdminContentSetAccessCommand access,
        CancellationToken cancellationToken = default)
    {
        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == flashcardSetId, cancellationToken);
        if (set == null)
        {
            return AdminContentSetDetailsResult.NotFound();
        }

        if (!set.IsPublic)
        {
            string? reasonError = ValidateAccessReason(access);
            if (reasonError != null)
            {
                return AdminContentSetDetailsResult.ReasonRequired(reasonError);
            }

            // Audit được ghi trước khi lấy danh sách thẻ để không có lần xem nội dung riêng tư thiếu dấu vết.
            await RecordPrivateDetailsAuditAsync(set, access, cancellationToken);
        }

        AdminContentSetDetails details = await BuildDetailsAsync(set, cancellationToken);
        return AdminContentSetDetailsResult.Success(details);
    }

    // Cách ly trực tiếp từ trang chi tiết hoặc danh sách nội dung.
    public async Task<ContentModerationOperationResult> QuarantineSetAsync(
        QuarantineFlashcardSetCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationMessage = ValidateQuarantineCommand(command);
        if (validationMessage != null)
        {
            return ContentModerationOperationResult.Failure(validationMessage);
        }

        FlashcardSet? set = await _context.FlashcardSets
            .SingleOrDefaultAsync(item => item.Id == command.FlashcardSetId, cancellationToken);
        if (set == null)
        {
            return ContentModerationOperationResult.Failure("Không tìm thấy bộ flashcard cần cách ly.");
        }

        ContentModerationOperationResult? denied =
            await DetectSetConflictOrClosedAsync(command, set, AdminAuditActions.ContentSetsQuarantine, cancellationToken);
        if (denied != null)
        {
            return denied;
        }

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
        string? validationMessage = ValidateReportQuarantineCommand(command);
        if (validationMessage != null)
        {
            return ContentModerationOperationResult.Failure(validationMessage);
        }

        ContentReport? report = await _context.ContentReports
            .Include(item => item.FlashcardSet)
            .SingleOrDefaultAsync(item => item.Id == command.ReportId, cancellationToken);
        if (report == null || report.FlashcardSet == null)
        {
            return ContentModerationOperationResult.Failure("Không tìm thấy báo cáo cần xử lý.");
        }

        ContentModerationOperationResult? denied =
            await DetectReportConflictOrClosedAsync(command, report, cancellationToken);
        if (denied != null)
        {
            return denied;
        }

        ContentModerationOperationResult? setDenied =
            await DetectSetConflictOrClosedAsync(command, report.FlashcardSet, AdminAuditActions.ContentReportsQuarantine, cancellationToken);
        if (setDenied != null)
        {
            return setDenied;
        }

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
        string? validationMessage = ValidateRestoreCommand(command);
        if (validationMessage != null)
        {
            return ContentModerationOperationResult.Failure(validationMessage);
        }

        FlashcardSet? set = await _context.FlashcardSets
            .SingleOrDefaultAsync(item => item.Id == command.FlashcardSetId, cancellationToken);
        if (set == null)
        {
            return ContentModerationOperationResult.Failure("Không tìm thấy bộ flashcard cần khôi phục.");
        }

        if (set.ModerationStatus != FlashcardSetModerationStatus.Quarantined)
        {
            const string message = "Bộ flashcard chưa bị cách ly nên không cần khôi phục.";
            await RecordSetDeniedAuditAsync(command, AdminAuditActions.ContentSetsRestore, set, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        if (set.ModerationVersion != command.Version)
        {
            const string message = "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            await RecordSetDeniedAuditAsync(command, AdminAuditActions.ContentSetsRestore, set, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        set.ModerationStatus = FlashcardSetModerationStatus.Active;
        set.ModerationPublicReason = null;
        set.ModerationInternalNote = null;
        set.ModerationEvidence = null;
        set.ModeratedByUserId = command.Actor.UserId.Trim();
        set.ModeratedAtUtc = nowUtc;
        set.ModerationVersion++;
        set.UpdatedAt = nowUtc;

        _auditService.Enqueue(BuildSetAuditEntry(
            command.Actor.UserId,
            command.Actor.Display,
            AdminAuditActions.ContentSetsRestore,
            AdminAuditOutcome.Success,
            set,
            command.Reason,
            command.Actor.CorrelationId,
            null));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ContentModerationOperationResult.Success("Đã khôi phục bộ flashcard.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            DetachAllTrackedEntities();
            await RecordSetDeniedAuditAsync(command, AdminAuditActions.ContentSetsRestore, set, "Xung đột phiên bản.", cancellationToken);
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
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        string normalizedReason = publicReason.Trim();

        set.ModerationStatus = FlashcardSetModerationStatus.Quarantined;
        set.ModerationPublicReason = normalizedReason;
        set.ModerationInternalNote = NormalizeOptional(internalNote);
        set.ModerationEvidence = NormalizeOptional(evidence);
        set.ModeratedByUserId = actorUserId.Trim();
        set.ModeratedAtUtc = nowUtc;
        set.ModerationVersion++;
        set.UpdatedAt = nowUtc;

        List<ContentReport> pendingReports = await _context.ContentReports
            .Where(report =>
                report.FlashcardSetId == set.Id
                && report.Status == ContentReportStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (ContentReport report in pendingReports)
        {
            report.Status = ContentReportStatus.Quarantined;
            report.ResolutionOutcome = ContentReportResolutionOutcome.Quarantined;
            report.ResolutionReason = normalizedReason;
            report.ResolvedByUserId = actorUserId.Trim();
            report.ResolvedAtUtc = nowUtc;
            report.Version++;
        }

        _auditService.Enqueue(BuildSetAuditEntry(
            actorUserId,
            actorDisplay,
            action,
            AdminAuditOutcome.Success,
            set,
            normalizedReason,
            correlationId,
            sourceReportId));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ContentModerationOperationResult.Success("Đã cách ly bộ flashcard.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            DetachAllTrackedEntities();
            await RecordSetDeniedAuditAsync(actorUserId, actorDisplay, action, set, normalizedReason, correlationId, "Xung đột phiên bản.", cancellationToken);
            return ContentModerationOperationResult.Failure("Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.");
        }
    }

    // Dựng dữ liệu chi tiết sau khi đã qua cổng lý do nếu bộ riêng tư.
    private async Task<AdminContentSetDetails> BuildDetailsAsync(
        FlashcardSet set,
        CancellationToken cancellationToken)
    {
        string ownerDisplay = await _context.AppUsers
            .AsNoTracking()
            .Where(user => user.Id == set.UserId)
            .Select(user => user.Email ?? user.UserName ?? user.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? set.UserId;

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
        if (string.IsNullOrWhiteSpace(search))
        {
            return sets;
        }

        string term = search.Trim();
        int? parsedSetId = null;
        if (int.TryParse(term, out int setId))
        {
            parsedSetId = setId;
        }

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
        string normalizedStatus = NormalizeToken(status);
        if (normalizedStatus == "quarantined")
        {
            return sets.Where(set => set.ModerationStatus == FlashcardSetModerationStatus.Quarantined);
        }

        if (normalizedStatus == "active")
        {
            return sets.Where(set => set.ModerationStatus == FlashcardSetModerationStatus.Active);
        }

        return sets;
    }

    // Lọc public/private cho Admin mà không mở nội dung thẻ.
    private static IQueryable<FlashcardSet> ApplyVisibilityFilter(
        IQueryable<FlashcardSet> sets,
        string? visibility)
    {
        string normalizedVisibility = NormalizeToken(visibility);
        if (normalizedVisibility == "public")
        {
            return sets.Where(set => set.IsPublic);
        }

        if (normalizedVisibility == "private")
        {
            return sets.Where(set => !set.IsPublic);
        }

        return sets;
    }

    // Phát hiện bộ đã cách ly hoặc form cũ trước khi ghi thay đổi.
    private async Task<ContentModerationOperationResult?> DetectSetConflictOrClosedAsync(
        QuarantineFlashcardSetCommand command,
        FlashcardSet set,
        string action,
        CancellationToken cancellationToken)
    {
        if (set.ModerationStatus == FlashcardSetModerationStatus.Quarantined)
        {
            const string message = "Bộ flashcard đã bị cách ly trước đó.";
            await RecordSetDeniedAuditAsync(command, action, set, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        if (set.ModerationVersion != command.Version)
        {
            const string message = "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            await RecordSetDeniedAuditAsync(command, action, set, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        return null;
    }

    // Phát hiện bộ đã cách ly hoặc form cũ khi thao tác đi từ báo cáo.
    private async Task<ContentModerationOperationResult?> DetectSetConflictOrClosedAsync(
        QuarantineFromReportCommand command,
        FlashcardSet set,
        string action,
        CancellationToken cancellationToken)
    {
        if (set.ModerationStatus == FlashcardSetModerationStatus.Quarantined)
        {
            const string message = "Bộ flashcard đã bị cách ly trước đó.";
            await RecordSetDeniedAuditAsync(command, action, set, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        if (set.ModerationVersion != command.FlashcardSetVersion)
        {
            const string message = "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            await RecordSetDeniedAuditAsync(command, action, set, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        return null;
    }

    // Phát hiện báo cáo đã xử lý hoặc form báo cáo cũ trước khi cách ly từ hàng đợi.
    private async Task<ContentModerationOperationResult?> DetectReportConflictOrClosedAsync(
        QuarantineFromReportCommand command,
        ContentReport report,
        CancellationToken cancellationToken)
    {
        if (report.Status != ContentReportStatus.Pending)
        {
            const string message = "Báo cáo này đã được xử lý trước đó.";
            await RecordReportDeniedAuditAsync(command, report, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        if (report.Version != command.ReportVersion)
        {
            const string message = "Báo cáo đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            await RecordReportDeniedAuditAsync(command, report, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        return null;
    }

    // Ghi audit cho lần Admin xem chi tiết nội dung riêng tư.
    private async Task RecordPrivateDetailsAuditAsync(
        FlashcardSet set,
        AdminContentSetAccessCommand access,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "private-flashcard-set",
            ["status"] = set.ModerationStatus
        };

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

        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Ghi audit từ chối cho thao tác nhắm vào báo cáo.
    private async Task RecordReportDeniedAuditAsync(
        QuarantineFromReportCommand command,
        ContentReport report,
        string denialReason,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "content-report",
            ["status"] = report.Status
        };

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
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "flashcard-set-moderation",
            ["status"] = set.ModerationStatus
        };

        if (sourceReportId != null)
        {
            metadata["filter"] = $"report:{sourceReportId.Value}";
        }

        if (denialReason != null)
        {
            metadata["deniedReason"] = denialReason;
        }

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
        if (command.ReportVersion <= 0)
        {
            return "Thiếu mã phiên bản báo cáo. Vui lòng tải lại trang.";
        }

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
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        if (!confirmed)
        {
            return "Vui lòng xác nhận trước khi cách ly bộ flashcard.";
        }

        string? publicReasonError = ValidateRequiredReason(publicReason, "Lý do công khai");
        if (publicReasonError != null)
        {
            return publicReasonError;
        }

        string? internalNoteError = ValidateOptionalLength(internalNote, "Ghi chú nội bộ");
        if (internalNoteError != null)
        {
            return internalNoteError;
        }

        string? evidenceError = ValidateOptionalLength(evidence, "Bằng chứng kiểm duyệt");
        if (evidenceError != null)
        {
            return evidenceError;
        }

        if (version <= 0)
        {
            return "Thiếu mã phiên bản bộ flashcard. Vui lòng tải lại trang.";
        }

        return null;
    }

    // Kiểm tra lệnh khôi phục trước khi ghi thay đổi.
    private static string? ValidateRestoreCommand(RestoreFlashcardSetCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Actor.UserId))
        {
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        if (!command.Confirmed)
        {
            return "Vui lòng xác nhận trước khi khôi phục bộ flashcard.";
        }

        string? reasonError = ValidateRequiredReason(command.Reason, "Lý do khôi phục");
        if (reasonError != null)
        {
            return reasonError;
        }

        if (command.Version <= 0)
        {
            return "Thiếu mã phiên bản bộ flashcard. Vui lòng tải lại trang.";
        }

        return null;
    }

    // Kiểm tra lý do mở nội dung riêng tư.
    private static string? ValidateAccessReason(AdminContentSetAccessCommand access)
    {
        if (string.IsNullOrWhiteSpace(access.Actor.UserId))
        {
            return "Không xác định được Quản trị viên đang xem.";
        }

        if (string.IsNullOrWhiteSpace(access.Reason))
        {
            return "Vui lòng nhập lý do trước khi xem nội dung riêng tư.";
        }

        if (access.Reason.Trim().Length > MaxPublicReasonLength)
        {
            return "Lý do không được vượt quá 500 ký tự.";
        }

        return null;
    }

    // Kiểm tra lý do bắt buộc và giới hạn độ dài.
    private static string? ValidateRequiredReason(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{fieldName} không được để trống.";
        }

        if (value.Trim().Length > MaxPublicReasonLength)
        {
            return $"{fieldName} không được vượt quá 500 ký tự.";
        }

        return null;
    }

    // Kiểm tra các ô nội bộ tùy chọn có vượt quá giới hạn lưu trữ hay không.
    private static string? ValidateOptionalLength(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Trim().Length > MaxInternalTextLength)
        {
            return $"{fieldName} không được vượt quá 1000 ký tự.";
        }

        return null;
    }

    // Chuẩn hóa token lọc từ query string.
    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant();
    }

    // Cắt khoảng trắng và chuyển chuỗi rỗng thành null.
    private static string? NormalizeOptional(string? value)
    {
        string? trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed;
    }

    // Gỡ entity tracked sau concurrency exception để DbContext không giữ trạng thái lỗi.
    private void DetachAllTrackedEntities()
    {
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry
                 in _context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
