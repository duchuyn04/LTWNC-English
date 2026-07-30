using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.ContentModeration;

public sealed class ContentReportModerationService : IContentModerationService
{
    private const int MaxReasonLength = 500;
    private const int MaxInternalTextLength = 1000;

    private readonly AppDbContext _context;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public ContentReportModerationService(
        AppDbContext context,
        IAdminAuditService auditService,
        TimeProvider timeProvider)
    {
        _context = context;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    public async Task<ContentModerationOperationResult> QuarantineFromReportAsync(
        QuarantineFromReportCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationError = ValidateQuarantine(command);
        if (validationError != null)
        {
            return ContentModerationOperationResult.Failure(validationError);
        }

        ContentReport? report = await _context.ContentReports
            .Include(item => item.FlashcardSet)
            .SingleOrDefaultAsync(item => item.Id == command.ReportId, cancellationToken);
        if (report?.FlashcardSet == null)
        {
            return ContentModerationOperationResult.Failure("Không tìm thấy báo cáo cần xử lý.");
        }

        if (report.Status != ContentReportStatus.Pending)
        {
            const string message = "Báo cáo này đã được xử lý trước đó.";
            await RecordReportDeniedAsync(command, report, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        if (report.Version != command.ReportVersion)
        {
            const string message = "Báo cáo đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trước khi thao tác.";
            await RecordReportDeniedAsync(command, report, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        FlashcardSet set = report.FlashcardSet;
        if (set.ModerationStatus == FlashcardSetModerationStatus.Quarantined)
        {
            const string message = "Bộ flashcard đã bị cách ly trước đó.";
            await RecordSetDeniedAsync(command, set, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        if (set.ModerationVersion != command.FlashcardSetVersion)
        {
            const string message = "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            await RecordSetDeniedAsync(command, set, message, cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        string publicReason = command.PublicReason.Trim();

        set.ModerationStatus = FlashcardSetModerationStatus.Quarantined;
        set.ModerationPublicReason = publicReason;
        set.ModerationInternalNote = NormalizeOptional(command.InternalNote);
        set.ModerationEvidence = NormalizeOptional(command.Evidence);
        set.ModeratedByUserId = command.Actor.UserId.Trim();
        set.ModeratedAtUtc = nowUtc;
        set.ModerationVersion++;
        set.UpdatedAt = nowUtc;

        List<ContentReport> pendingReports = await _context.ContentReports
            .Where(item => item.FlashcardSetId == set.Id
                && item.Status == ContentReportStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (ContentReport pendingReport in pendingReports)
        {
            pendingReport.Status = ContentReportStatus.Quarantined;
            pendingReport.ResolutionOutcome = ContentReportResolutionOutcome.Quarantined;
            pendingReport.ResolutionReason = publicReason;
            pendingReport.ResolvedByUserId = command.Actor.UserId.Trim();
            pendingReport.ResolvedAtUtc = nowUtc;
            pendingReport.Version++;
        }

        _auditService.Enqueue(BuildSetAudit(
            command.Actor,
            AdminAuditActions.ContentReportsQuarantine,
            AdminAuditOutcome.Success,
            set,
            publicReason,
            command.ReportId));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ContentModerationOperationResult.Success("Đã cách ly bộ flashcard.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            DetachTrackedEntities();
            await RecordSetDeniedAsync(
                command.Actor,
                AdminAuditActions.ContentReportsQuarantine,
                set,
                publicReason,
                "Xung đột phiên bản.",
                cancellationToken);
            return ContentModerationOperationResult.Failure(
                "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.");
        }
    }

    public async Task<ContentModerationOperationResult> RestoreSetAsync(
        RestoreFlashcardSetCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationError = ValidateRestore(command);
        if (validationError != null)
        {
            return ContentModerationOperationResult.Failure(validationError);
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
            await RecordSetDeniedAsync(
                command.Actor,
                AdminAuditActions.ContentSetsRestore,
                set,
                command.Reason,
                message,
                cancellationToken);
            return ContentModerationOperationResult.Failure(message);
        }

        if (set.ModerationVersion != command.Version)
        {
            const string message = "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.";
            await RecordSetDeniedAsync(
                command.Actor,
                AdminAuditActions.ContentSetsRestore,
                set,
                command.Reason,
                message,
                cancellationToken);
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

        _auditService.Enqueue(BuildSetAudit(
            command.Actor,
            AdminAuditActions.ContentSetsRestore,
            AdminAuditOutcome.Success,
            set,
            command.Reason));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ContentModerationOperationResult.Success("Đã khôi phục bộ flashcard.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            DetachTrackedEntities();
            await RecordSetDeniedAsync(
                command.Actor,
                AdminAuditActions.ContentSetsRestore,
                set,
                command.Reason,
                "Xung đột phiên bản.",
                cancellationToken);
            return ContentModerationOperationResult.Failure(
                "Bộ flashcard đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trang.");
        }
    }

    private async Task RecordReportDeniedAsync(
        QuarantineFromReportCommand command,
        ContentReport report,
        string denialReason,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AdminAuditEntry(
            command.Actor.UserId,
            command.Actor.Display,
            AdminAuditActions.ContentReportsQuarantine,
            AdminAuditOutcome.Denied,
            "ContentReport",
            report.Id.ToString(),
            command.PublicReason,
            command.Actor.CorrelationId,
            new Dictionary<string, string?>
            {
                ["scope"] = "content-report",
                ["status"] = report.Status,
                ["deniedReason"] = denialReason
            }), cancellationToken);
    }

    private Task RecordSetDeniedAsync(
        QuarantineFromReportCommand command,
        FlashcardSet set,
        string denialReason,
        CancellationToken cancellationToken)
    {
        return RecordSetDeniedAsync(
            command.Actor,
            AdminAuditActions.ContentReportsQuarantine,
            set,
            command.PublicReason,
            denialReason,
            cancellationToken);
    }

    private async Task RecordSetDeniedAsync(
        AdminActorContext actor,
        string action,
        FlashcardSet set,
        string reason,
        string denialReason,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(BuildSetAudit(
            actor,
            action,
            AdminAuditOutcome.Denied,
            set,
            reason,
            denialReason: denialReason), cancellationToken);
    }

    private static AdminAuditEntry BuildSetAudit(
        AdminActorContext actor,
        string action,
        string outcome,
        FlashcardSet set,
        string reason,
        long? sourceReportId = null,
        string? denialReason = null)
    {
        var metadata = new Dictionary<string, string?>
        {
            ["scope"] = "flashcard-set-moderation",
            ["status"] = set.ModerationStatus
        };
        if (sourceReportId.HasValue)
        {
            metadata["filter"] = $"report:{sourceReportId.Value}";
        }

        if (denialReason != null)
        {
            metadata["deniedReason"] = denialReason;
        }

        return new AdminAuditEntry(
            actor.UserId,
            actor.Display,
            action,
            outcome,
            "FlashcardSet",
            set.Id.ToString(),
            reason,
            actor.CorrelationId,
            metadata);
    }

    private static string? ValidateQuarantine(QuarantineFromReportCommand command)
    {
        if (command.ReportVersion <= 0)
        {
            return "Thiếu mã phiên bản báo cáo. Vui lòng tải lại trang.";
        }

        if (command.FlashcardSetVersion <= 0)
        {
            return "Thiếu mã phiên bản bộ flashcard. Vui lòng tải lại trang.";
        }

        if (!command.Confirmed)
        {
            return "Vui lòng xác nhận trước khi cách ly bộ flashcard.";
        }

        string? commonError = ValidateActorAndReason(command.Actor, command.PublicReason, "Lý do công khai");
        if (commonError != null)
        {
            return commonError;
        }

        return ValidateOptional(command.InternalNote, "Ghi chú nội bộ")
            ?? ValidateOptional(command.Evidence, "Bằng chứng kiểm duyệt");
    }

    private static string? ValidateRestore(RestoreFlashcardSetCommand command)
    {
        if (command.Version <= 0)
        {
            return "Thiếu mã phiên bản bộ flashcard. Vui lòng tải lại trang.";
        }

        if (!command.Confirmed)
        {
            return "Vui lòng xác nhận trước khi khôi phục bộ flashcard.";
        }

        return ValidateActorAndReason(command.Actor, command.Reason, "Lý do khôi phục");
    }

    private static string? ValidateActorAndReason(
        AdminActorContext actor,
        string reason,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(actor.UserId))
        {
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return $"{fieldName} không được để trống.";
        }

        return reason.Trim().Length > MaxReasonLength
            ? $"{fieldName} không được vượt quá {MaxReasonLength} ký tự."
            : null;
    }

    private static string? ValidateOptional(string? value, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Trim().Length > MaxInternalTextLength
            ? $"{fieldName} không được vượt quá {MaxInternalTextLength} ký tự."
            : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private void DetachTrackedEntities()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
