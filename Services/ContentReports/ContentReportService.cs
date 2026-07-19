using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.ContentReports;

public sealed class ContentReportService : IContentReportService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    private const int MaxDescriptionLength = 1000;
    private const int MaxResolutionReasonLength = 500;

    private static readonly IReadOnlyList<ContentReportReasonOption> ReasonOptions =
    [
        new("spam", "Spam hoặc quảng cáo"),
        new("offensive", "Ngôn từ xúc phạm"),
        new("unsafe", "Nội dung không an toàn"),
        new("copyright", "Vi phạm bản quyền"),
        new("incorrect", "Thông tin sai lệch"),
        new("other", "Lý do khác")
    ];

    private readonly AppDbContext _context;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    // Nhận DbContext, audit và đồng hồ để mọi thời điểm đều kiểm thử được.
    public ContentReportService(
        AppDbContext context,
        IAdminAuditService auditService,
        TimeProvider timeProvider)
    {
        _context = context;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    // Trả danh mục lý do cố định cho form người học và bộ lọc Admin.
    public IReadOnlyList<ContentReportReasonOption> GetReasonOptions()
    {
        return ReasonOptions;
    }

    // Kiểm tra nhanh báo cáo đang mở của cùng người/cùng bộ để UI biết có nên hiện form hay không.
    public async Task<bool> HasOpenReportAsync(
        int flashcardSetId,
        string reporterUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reporterUserId))
        {
            return false;
        }

        return await _context.ContentReports
            .AsNoTracking()
            .AnyAsync(report =>
                report.FlashcardSetId == flashcardSetId
                && report.ReporterUserId == reporterUserId
                && report.Status == ContentReportStatus.Pending,
                cancellationToken);
    }

    // Tạo báo cáo mới sau khi kiểm tra bộ công khai, không tự báo cáo và không trùng báo cáo đang mở.
    public async Task<ContentReportSubmitResult> SubmitAsync(
        SubmitContentReportCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationMessage = ValidateSubmitCommand(command);
        if (validationMessage != null)
        {
            return ContentReportSubmitResult.Rejected(
                validationMessage,
                ContentReportSubmitFailure.Validation);
        }

        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.FlashcardSetId, cancellationToken);
        if (set == null || !set.IsPublic)
        {
            return ContentReportSubmitResult.Rejected(
                "Chỉ có thể báo cáo bộ flashcard công khai đang tồn tại.",
                ContentReportSubmitFailure.NotFoundOrPrivate);
        }

        if (string.Equals(set.UserId, command.ReporterUserId, StringComparison.Ordinal))
        {
            return ContentReportSubmitResult.Rejected(
                "Bạn không thể báo cáo bộ flashcard của chính mình.",
                ContentReportSubmitFailure.SelfReport);
        }

        bool hasOpenReport = await HasOpenReportAsync(
            command.FlashcardSetId,
            command.ReporterUserId,
            cancellationToken);
        if (hasOpenReport)
        {
            return ContentReportSubmitResult.Rejected(
                "Bạn đã có một báo cáo đang chờ xử lý cho bộ flashcard này.",
                ContentReportSubmitFailure.DuplicateOpenReport);
        }

        ContentReport report = new()
        {
            FlashcardSetId = command.FlashcardSetId,
            ReporterUserId = command.ReporterUserId.Trim(),
            Reason = NormalizeReason(command.Reason),
            Description = NormalizeOptional(command.Description),
            Status = ContentReportStatus.Pending,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            Version = 1
        };

        _context.ContentReports.Add(report);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return ContentReportSubmitResult.Success(report.Id);
        }
        catch (DbUpdateException)
        {
            // Chỉ mục unique filtered có thể bắt race khi hai request gửi cùng lúc.
            DetachPendingContentReport(report);
            bool duplicateExists = await HasOpenReportAsync(
                command.FlashcardSetId,
                command.ReporterUserId,
                cancellationToken);
            if (duplicateExists)
            {
                return ContentReportSubmitResult.Rejected(
                    "Bạn đã có một báo cáo đang chờ xử lý cho bộ flashcard này.",
                    ContentReportSubmitFailure.DuplicateOpenReport);
            }

            throw;
        }
    }

    // Tìm kiếm hàng đợi Admin với lọc, sắp xếp và phân trang hoàn toàn phía máy chủ.
    public async Task<AdminContentReportPage> SearchForAdminAsync(
        AdminContentReportQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(DefaultPage, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        IQueryable<ContentReport> reports = _context.ContentReports.AsNoTracking();
        reports = ApplyStatusFilter(reports, query.Status);
        reports = ApplyReasonFilter(reports, query.Reason);
        reports = ApplySearchFilter(reports, query.Search);
        reports = ApplySort(reports, query.Sort);

        int totalCount = await reports.CountAsync(cancellationToken);
        List<AdminContentReportRowData> rowData = await BuildAdminRows(reports)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        List<AdminContentReportRow> items = rowData
            .Select(ToAdminRow)
            .ToList();

        return new AdminContentReportPage(items, totalCount, page, pageSize);
    }

    // Đếm báo cáo đang chờ quá ngưỡng tuổi để dashboard/cảnh báo issue sau truy vấn lại được.
    public async Task<int> CountPendingOlderThanAsync(
        TimeSpan age,
        CancellationToken cancellationToken = default)
    {
        DateTime threshold = _timeProvider.GetUtcNow().UtcDateTime.Subtract(age);
        return await _context.ContentReports
            .AsNoTracking()
            .CountAsync(report =>
                report.Status == ContentReportStatus.Pending
                && report.CreatedAtUtc <= threshold,
                cancellationToken);
    }

    // Bác bỏ báo cáo đang chờ, ghi lý do xử lý và audit trong cùng transaction.
    public async Task<ContentReportOperationResult> DismissAsync(
        DismissContentReportCommand command,
        CancellationToken cancellationToken = default)
    {
        string? validationMessage = ValidateDismissCommand(command);
        if (validationMessage != null)
        {
            return ContentReportOperationResult.Failure(validationMessage);
        }

        ContentReport? report = await _context.ContentReports
            .Include(item => item.FlashcardSet)
            .SingleOrDefaultAsync(item => item.Id == command.ReportId, cancellationToken);
        if (report == null)
        {
            return ContentReportOperationResult.Failure("Không tìm thấy báo cáo cần xử lý.");
        }

        ContentReportOperationResult? deniedResult = await DetectDismissConflictOrClosedAsync(
            command,
            report,
            cancellationToken);
        if (deniedResult != null)
        {
            return deniedResult;
        }

        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        report.Status = ContentReportStatus.Dismissed;
        report.ResolutionOutcome = ContentReportResolutionOutcome.Dismissed;
        report.ResolutionReason = command.Reason.Trim();
        report.ResolvedByUserId = command.ActorUserId.Trim();
        report.ResolvedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        report.Version++;

        _auditService.Enqueue(BuildAuditEntry(
            command,
            AdminAuditOutcome.Success,
            report,
            null));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ContentReportOperationResult.Success("Đã bác bỏ báo cáo nội dung.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            DetachAllTrackedEntities();
            await RecordDismissDeniedAuditAsync(
                command,
                report,
                "Báo cáo đã thay đổi bởi yêu cầu khác.",
                cancellationToken);
            return ContentReportOperationResult.Failure(
                "Báo cáo đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trước khi thao tác.");
        }
    }

    // Dựng query projection cho bảng Admin, ghép người gửi/chủ sở hữu mà không load toàn bộ dữ liệu lên bộ nhớ.
    private IQueryable<AdminContentReportRowData> BuildAdminRows(IQueryable<ContentReport> reports)
    {
        return from report in reports
               join set in _context.FlashcardSets on report.FlashcardSetId equals set.Id
               join reporter in _context.Users on report.ReporterUserId equals reporter.Id
               join owner in _context.Users on set.UserId equals owner.Id
               select new AdminContentReportRowData(
                   report.Id,
                   set.Id,
                   set.Title,
                   reporter.Id,
                   reporter.Email ?? reporter.UserName ?? reporter.Id,
                   owner.Id,
                   owner.Email ?? owner.UserName ?? owner.Id,
                   report.Reason,
                   report.Description,
                   report.Status,
                   report.CreatedAtUtc,
                   report.ResolvedAtUtc,
                   report.ResolutionReason,
                   report.Version);
    }

    // Gắn nhãn lý do sau khi EF đã lấy dữ liệu để tránh dịch method C# vào SQL.
    private static AdminContentReportRow ToAdminRow(AdminContentReportRowData row)
    {
        return new AdminContentReportRow(
            row.Id,
            row.FlashcardSetId,
            row.FlashcardSetTitle,
            row.ReporterUserId,
            row.ReporterDisplay,
            row.OwnerUserId,
            row.OwnerDisplay,
            row.Reason,
            ToReasonLabel(row.Reason),
            row.Description,
            row.Status,
            row.CreatedAtUtc,
            row.ResolvedAtUtc,
            row.ResolutionReason,
            row.Version);
    }

    // Lọc trạng thái; mặc định là Pending vì đây là trang hàng đợi xử lý.
    private static IQueryable<ContentReport> ApplyStatusFilter(
        IQueryable<ContentReport> reports,
        string? status)
    {
        string normalizedStatus = NormalizeToken(status);
        if (string.IsNullOrWhiteSpace(normalizedStatus)
            || normalizedStatus == "pending")
        {
            return reports.Where(report => report.Status == ContentReportStatus.Pending);
        }

        if (normalizedStatus == "all")
        {
            return reports;
        }

        if (normalizedStatus == "dismissed")
        {
            return reports.Where(report => report.Status == ContentReportStatus.Dismissed);
        }

        if (normalizedStatus == "quarantined")
        {
            return reports.Where(report => report.Status == ContentReportStatus.Quarantined);
        }

        return reports.Where(report => report.Status == ContentReportStatus.Pending);
    }

    // Lọc theo danh mục lý do cố định; giá trị lạ bị bỏ qua để không tạo query tùy ý.
    private static IQueryable<ContentReport> ApplyReasonFilter(
        IQueryable<ContentReport> reports,
        string? reason)
    {
        string normalizedReason = NormalizeReason(reason);
        if (!IsValidReason(normalizedReason))
        {
            return reports;
        }

        return reports.Where(report => report.Reason == normalizedReason);
    }

    // Tìm kiếm theo mã báo cáo, mã bộ, tiêu đề bộ và email người liên quan.
    private IQueryable<ContentReport> ApplySearchFilter(
        IQueryable<ContentReport> reports,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return reports;
        }

        string term = search.Trim();
        long? reportId = null;
        if (long.TryParse(term, out long parsedReportId))
        {
            reportId = parsedReportId;
        }

        int? flashcardSetId = null;
        if (int.TryParse(term, out int parsedFlashcardSetId))
        {
            flashcardSetId = parsedFlashcardSetId;
        }

        return reports.Where(report =>
            (reportId != null && report.Id == reportId.Value)
            || (flashcardSetId != null && report.FlashcardSetId == flashcardSetId.Value)
            || _context.FlashcardSets.Any(set =>
                set.Id == report.FlashcardSetId
                && set.Title.Contains(term))
            || _context.Users.Any(user =>
                (user.Id == report.ReporterUserId
                    || _context.FlashcardSets.Any(set =>
                        set.Id == report.FlashcardSetId
                        && set.UserId == user.Id))
                && ((user.Email != null && user.Email.Contains(term))
                    || (user.UserName != null && user.UserName.Contains(term))
                    || user.Id.Contains(term))));
    }

    // Sắp xếp bằng danh sách khóa cố định để tránh truyền tên cột trực tiếp từ query string.
    private static IQueryable<ContentReport> ApplySort(
        IQueryable<ContentReport> reports,
        string? sort)
    {
        string normalizedSort = NormalizeToken(sort);
        if (normalizedSort == "oldest")
        {
            return reports.OrderBy(report => report.CreatedAtUtc).ThenBy(report => report.Id);
        }

        if (normalizedSort == "reason")
        {
            return reports.OrderBy(report => report.Reason)
                .ThenByDescending(report => report.CreatedAtUtc)
                .ThenByDescending(report => report.Id);
        }

        if (normalizedSort == "status")
        {
            return reports.OrderBy(report => report.Status)
                .ThenByDescending(report => report.CreatedAtUtc)
                .ThenByDescending(report => report.Id);
        }

        return reports.OrderByDescending(report => report.CreatedAtUtc)
            .ThenByDescending(report => report.Id);
    }

    // Phát hiện form cũ hoặc báo cáo đã được xử lý trước khi cho phép bác bỏ.
    private async Task<ContentReportOperationResult?> DetectDismissConflictOrClosedAsync(
        DismissContentReportCommand command,
        ContentReport report,
        CancellationToken cancellationToken)
    {
        if (report.Status != ContentReportStatus.Pending)
        {
            const string message = "Báo cáo này đã được xử lý trước đó.";
            await RecordDismissDeniedAuditAsync(command, report, message, cancellationToken);
            return ContentReportOperationResult.Failure(message);
        }

        if (report.Version != command.Version)
        {
            const string message = "Báo cáo đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trước khi thao tác.";
            await RecordDismissDeniedAuditAsync(command, report, message, cancellationToken);
            return ContentReportOperationResult.Failure(message);
        }

        return null;
    }

    // Ghi audit riêng cho nhánh bị từ chối vì không có thay đổi nghiệp vụ để gộp transaction.
    private async Task RecordDismissDeniedAuditAsync(
        DismissContentReportCommand command,
        ContentReport report,
        string denialReason,
        CancellationToken cancellationToken)
    {
        AdminAuditEntry entry = BuildAuditEntry(
            command,
            AdminAuditOutcome.Denied,
            report,
            denialReason);
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Tạo payload audit an toàn, chỉ gồm mã báo cáo, mã bộ và lý do nghiệp vụ.
    private static AdminAuditEntry BuildAuditEntry(
        DismissContentReportCommand command,
        string outcome,
        ContentReport report,
        string? denialReason)
    {
        var metadata = new Dictionary<string, string?>
        {
            ["FlashcardSetId"] = report.FlashcardSetId.ToString(),
            ["ReportReason"] = report.Reason,
            ["ReportStatus"] = report.Status,
            ["DeniedReason"] = denialReason
        };

        return new AdminAuditEntry(
            ActorUserId: command.ActorUserId,
            ActorDisplay: command.ActorDisplay,
            Action: AdminAuditActions.ContentReportsDismiss,
            Outcome: outcome,
            TargetType: "ContentReport",
            TargetId: report.Id.ToString(),
            Reason: command.Reason,
            CorrelationId: command.CorrelationId,
            Metadata: metadata);
    }

    // Kiểm tra lệnh gửi báo cáo trước khi đụng database.
    private static string? ValidateSubmitCommand(SubmitContentReportCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ReporterUserId))
        {
            return "Vui lòng đăng nhập trước khi báo cáo nội dung.";
        }

        string reason = NormalizeReason(command.Reason);
        if (!IsValidReason(reason))
        {
            return "Vui lòng chọn lý do báo cáo hợp lệ.";
        }

        string? description = NormalizeOptional(command.Description);
        if (description != null && description.Length > MaxDescriptionLength)
        {
            return $"Mô tả không được vượt quá {MaxDescriptionLength} ký tự.";
        }

        return null;
    }

    // Kiểm tra lệnh bác bỏ báo cáo trước khi ghi thay đổi nhạy cảm.
    private static string? ValidateDismissCommand(DismissContentReportCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ActorUserId))
        {
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return "Vui lòng nhập lý do xử lý trước khi bác bỏ báo cáo.";
        }

        if (command.Reason.Trim().Length > MaxResolutionReasonLength)
        {
            return $"Lý do xử lý không được vượt quá {MaxResolutionReasonLength} ký tự.";
        }

        if (command.Version <= 0)
        {
            return "Thiếu mã phiên bản báo cáo. Vui lòng tải lại trang.";
        }

        return null;
    }

    // Đưa reason về khóa ổn định để so sánh và lưu database.
    private static string NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return string.Empty;
        }

        return reason.Trim().ToLowerInvariant();
    }

    // Chuẩn hóa token lọc/sắp xếp nhập từ query string.
    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant();
    }

    // Cắt khoảng trắng và chuyển chuỗi rỗng thành null cho mô tả/lý do tùy chọn.
    private static string? NormalizeOptional(string? value)
    {
        string? trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed;
    }

    // Kiểm tra reason có nằm trong danh mục cố định của phiên bản 1 hay không.
    private static bool IsValidReason(string reason)
    {
        foreach (ContentReportReasonOption option in ReasonOptions)
        {
            if (string.Equals(option.Value, reason, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Đổi reason code sang nhãn tiếng Việt để service trả dữ liệu dễ dùng cho view.
    private static string ToReasonLabel(string reason)
    {
        foreach (ContentReportReasonOption option in ReasonOptions)
        {
            if (string.Equals(option.Value, reason, StringComparison.Ordinal))
            {
                return option.Label;
            }
        }

        return "Lý do khác";
    }

    // Gỡ entity báo cáo vừa add khi unique index báo trùng để DbContext dùng tiếp được.
    private void DetachPendingContentReport(ContentReport report)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ContentReport> entry =
            _context.Entry(report);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }

    // Gỡ toàn bộ entity tracked sau concurrency exception để tránh lưu lại trạng thái lỗi.
    private void DetachAllTrackedEntities()
    {
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry
                 in _context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private sealed record AdminContentReportRowData(
        long Id,
        int FlashcardSetId,
        string FlashcardSetTitle,
        string ReporterUserId,
        string ReporterDisplay,
        string OwnerUserId,
        string OwnerDisplay,
        string Reason,
        string? Description,
        string Status,
        DateTime CreatedAtUtc,
        DateTime? ResolvedAtUtc,
        string? ResolutionReason,
        int Version);
}
