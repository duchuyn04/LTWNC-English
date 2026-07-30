using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ltwnc.Services.ContentReports;

public sealed class ContentReportService : IContentReportService
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;

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
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_auditService` để các phương thức khác sử dụng.
        _auditService = auditService;
        // 3. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    // Trả danh mục lý do cố định cho form người học và bộ lọc Admin.
    public IReadOnlyList<ContentReportReasonOption> GetReasonOptions()
    {
        // 1. Trả `ReasonOptions` cho nơi gọi.
        return ReasonOptions;
    }

    // Kiểm tra nhanh báo cáo đang mở của cùng người/cùng bộ để UI biết có nên hiện form hay không.
    public async Task<bool> HasOpenReportAsync(
        int flashcardSetId,
        string reporterUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(reporterUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(reporterUserId))
        {
            // 2. Trả `false` cho nơi gọi.
            return false;
        }

        // 3. Trả kết quả từ `AnyAsync` cho nơi gọi.
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
        // 1. Gọi `ValidateSubmitCommand` và lưu kết quả vào `validationMessage`.
        string? validationMessage = ValidateSubmitCommand(command);
        // 2. Kiểm tra `validationMessage != null` để chọn nhánh xử lý phù hợp.
        if (validationMessage != null)
        {
            // 3. Trả kết quả từ `Rejected` cho nơi gọi.
            return ContentReportSubmitResult.Rejected(
                validationMessage,
                ContentReportSubmitFailure.Validation);
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == command.FlashcardSetId, cancellationToken);
        // 5. Kiểm tra `set == null || !set.IsPublic || set.ModerationStatus != FlashcardSe...` để chọn nhánh xử lý phù hợp.
        if (set == null
            || !set.IsPublic
            || set.ModerationStatus != FlashcardSetModerationStatus.Active)
        {
            // 6. Trả kết quả từ `Rejected` cho nơi gọi.
            return ContentReportSubmitResult.Rejected(
                "Chỉ có thể báo cáo bộ flashcard công khai đang tồn tại.",
                ContentReportSubmitFailure.NotFoundOrPrivate);
        }

        // 7. Kiểm tra `string.Equals(set.UserId, command.ReporterUserId, StringComparison....` để chọn nhánh xử lý phù hợp.
        if (string.Equals(set.UserId, command.ReporterUserId, StringComparison.Ordinal))
        {
            // 8. Trả kết quả từ `Rejected` cho nơi gọi.
            return ContentReportSubmitResult.Rejected(
                "Bạn không thể báo cáo bộ flashcard của chính mình.",
                ContentReportSubmitFailure.SelfReport);
        }

        // 9. Gọi `HasOpenReportAsync` và lưu kết quả vào `hasOpenReport`.
        bool hasOpenReport = await HasOpenReportAsync(
            command.FlashcardSetId,
            command.ReporterUserId,
            cancellationToken);
        // 10. Kiểm tra `hasOpenReport` để chọn nhánh xử lý phù hợp.
        if (hasOpenReport)
        {
            // 11. Trả kết quả từ `Rejected` cho nơi gọi.
            return ContentReportSubmitResult.Rejected(
                "Bạn đã có một báo cáo đang chờ xử lý cho bộ flashcard này.",
                ContentReportSubmitFailure.DuplicateOpenReport);
        }

        // 12. Khởi tạo `report` với dữ liệu ban đầu cần thiết.
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

        // 13. Gọi `Add` để thực hiện bước nghiệp vụ này.
        _context.ContentReports.Add(report);
        // 14. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 15. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 16. Trả kết quả từ `Success` cho nơi gọi.
            return ContentReportSubmitResult.Success(report.Id);
        }
        catch (DbUpdateException)
        {
            // Chỉ mục unique filtered có thể bắt race khi hai request gửi cùng lúc.
            // 17. Gọi `DetachPendingContentReport` để thực hiện bước nghiệp vụ này.
            DetachPendingContentReport(report);
            // 18. Gọi `HasOpenReportAsync` và lưu kết quả vào `duplicateExists`.
            bool duplicateExists = await HasOpenReportAsync(
                command.FlashcardSetId,
                command.ReporterUserId,
                cancellationToken);
            // 19. Kiểm tra `duplicateExists` để chọn nhánh xử lý phù hợp.
            if (duplicateExists)
            {
                // 20. Trả kết quả từ `Rejected` cho nơi gọi.
                return ContentReportSubmitResult.Rejected(
                    "Bạn đã có một báo cáo đang chờ xử lý cho bộ flashcard này.",
                    ContentReportSubmitFailure.DuplicateOpenReport);
            }

            // 21. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
    }

    // Tìm kiếm hàng đợi Admin với lọc, sắp xếp và phân trang hoàn toàn phía máy chủ.
    public async Task<AdminContentReportPage> SearchForAdminAsync(
        AdminContentReportQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `Max` và lưu kết quả vào `page`.
        int page = Math.Max(DefaultPage, query.Page);
        // 2. Gọi `Clamp` và lưu kết quả vào `pageSize`.
        int pageSize = DefaultPageSize;

        // 3. Gọi `AsNoTracking` và lưu kết quả vào `reports`.
        IQueryable<ContentReport> reports = _context.ContentReports.AsNoTracking();
        // 4. Cập nhật `reports` bằng giá trị mới.
        reports = ApplyStatusFilter(reports, query.Status);
        reports = reports.OrderBy(report => report.CreatedAtUtc).ThenBy(report => report.Id);

        // 8. Gọi `CountAsync` và lưu kết quả vào `totalCount`.
        int totalCount = await reports.CountAsync(cancellationToken);
        // 9. Gọi `ToListAsync` và lưu kết quả vào `rowData`.
        List<AdminContentReportRowData> rowData = await BuildAdminRows(reports)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        // 10. Gọi `ToList` và lưu kết quả vào `items`.
        List<AdminContentReportRow> items = rowData
            .Select(ToAdminRow)
            .ToList();

        // 11. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminContentReportPage(items, totalCount, page, pageSize);
    }

    // Bác bỏ báo cáo đang chờ, ghi lý do xử lý và audit trong cùng transaction.
    public async Task<ContentReportOperationResult> DismissAsync(
        DismissContentReportCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Gọi `ValidateDismissCommand` và lưu kết quả vào `validationMessage`.
        string? validationMessage = ValidateDismissCommand(command);
        // 2. Kiểm tra `validationMessage != null` để chọn nhánh xử lý phù hợp.
        if (validationMessage != null)
        {
            // 3. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentReportOperationResult.Failure(validationMessage);
        }

        // 4. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `report`.
        ContentReport? report = await _context.ContentReports
            .Include(item => item.FlashcardSet)
            .SingleOrDefaultAsync(item => item.Id == command.ReportId, cancellationToken);
        // 5. Kiểm tra `report == null` để chọn nhánh xử lý phù hợp.
        if (report == null)
        {
            // 6. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentReportOperationResult.Failure("Không tìm thấy báo cáo cần xử lý.");
        }

        // 7. Gọi `DetectDismissConflictOrClosedAsync` và lưu kết quả vào `deniedResult`.
        ContentReportOperationResult? deniedResult = await DetectDismissConflictOrClosedAsync(
            command,
            report,
            cancellationToken);
        // 8. Kiểm tra `deniedResult != null` để chọn nhánh xử lý phù hợp.
        if (deniedResult != null)
        {
            // 9. Trả `deniedResult` cho nơi gọi.
            return deniedResult;
        }

        // 10. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // 11. Cập nhật `report.Status` bằng giá trị mới.
        report.Status = ContentReportStatus.Dismissed;
        // 12. Cập nhật `report.ResolutionOutcome` bằng giá trị mới.
        report.ResolutionOutcome = ContentReportResolutionOutcome.Dismissed;
        // 13. Cập nhật `report.ResolutionReason` bằng giá trị mới.
        report.ResolutionReason = command.Reason.Trim();
        // 14. Cập nhật `report.ResolvedByUserId` bằng giá trị mới.
        report.ResolvedByUserId = command.Actor.UserId.Trim();
        // 15. Cập nhật `report.ResolvedAtUtc` bằng giá trị mới.
        report.ResolvedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        // 16. Cập nhật bộ đếm hoặc trạng thái `report.Version`.
        report.Version++;

        // 17. Gọi `Enqueue` để thực hiện bước nghiệp vụ này.
        _auditService.Enqueue(BuildAuditEntry(
            command,
            AdminAuditOutcome.Success,
            report,
            null));

        // 18. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 19. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync(cancellationToken);
            // 20. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
            await transaction.CommitAsync(cancellationToken);
            // 21. Trả kết quả từ `Success` cho nơi gọi.
            return ContentReportOperationResult.Success("Đã bác bỏ báo cáo nội dung.");
        }
        catch (DbUpdateConcurrencyException)
        {
            // 22. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
            await transaction.RollbackAsync(cancellationToken);
            // 23. Gọi `DetachAllTrackedEntities` để thực hiện bước nghiệp vụ này.
            DetachAllTrackedEntities();
            // 24. Gọi `RecordDismissDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordDismissDeniedAuditAsync(
                command,
                report,
                "Báo cáo đã thay đổi bởi yêu cầu khác.",
                cancellationToken);
            // 25. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentReportOperationResult.Failure(
                "Báo cáo đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trước khi thao tác.");
        }
    }

    // Dựng query projection cho bảng Admin, ghép người gửi/chủ sở hữu mà không load toàn bộ dữ liệu lên bộ nhớ.
    private IQueryable<AdminContentReportRowData> BuildAdminRows(IQueryable<ContentReport> reports)
    {
        // 1. Trả `from report in reports join set in _context.FlashcardSets on report...` cho nơi gọi.
        return from report in reports
               join set in _context.FlashcardSets on report.FlashcardSetId equals set.Id
               join reporter in _context.AppUsers on report.ReporterUserId equals reporter.Id
               join owner in _context.AppUsers on set.UserId equals owner.Id
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
                   report.Version,
                   set.ModerationVersion);
    }

    // Gắn nhãn lý do sau khi EF đã lấy dữ liệu để tránh dịch method C# vào SQL.
    private static AdminContentReportRow ToAdminRow(AdminContentReportRowData row)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
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
            row.Version,
            row.FlashcardSetVersion);
    }

    // Lọc trạng thái; mặc định là Pending vì đây là trang hàng đợi xử lý.
    private IQueryable<ContentReport> ApplyStatusFilter(
        IQueryable<ContentReport> reports,
        string? status)
    {
        // 1. Gọi `NormalizeToken` và lưu kết quả vào `normalizedStatus`.
        string normalizedStatus = NormalizeToken(status);
        // 2. Kiểm tra `string.IsNullOrWhiteSpace(normalizedStatus) || normalizedStatus == ...` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(normalizedStatus)
            || normalizedStatus == "pending")
        {
            // 3. Trả kết quả từ `Where` cho nơi gọi.
            return reports.Where(report => report.Status == ContentReportStatus.Pending);
        }

        if (normalizedStatus == "quarantined")
        {
            return reports.Where(report =>
                report.Status == ContentReportStatus.Quarantined
                && _context.FlashcardSets.Any(set =>
                    set.Id == report.FlashcardSetId
                    && set.ModerationStatus == FlashcardSetModerationStatus.Quarantined));
        }

        // 10. Trả kết quả từ `Where` cho nơi gọi.
        return reports.Where(report => report.Status == ContentReportStatus.Pending);
    }

    // Phát hiện form cũ hoặc báo cáo đã được xử lý trước khi cho phép bác bỏ.
    private async Task<ContentReportOperationResult?> DetectDismissConflictOrClosedAsync(
        DismissContentReportCommand command,
        ContentReport report,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra `report.Status != ContentReportStatus.Pending` để chọn nhánh xử lý phù hợp.
        if (report.Status != ContentReportStatus.Pending)
        {
            // 2. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Báo cáo này đã được xử lý trước đó.";
            // 3. Gọi `RecordDismissDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordDismissDeniedAuditAsync(command, report, message, cancellationToken);
            // 4. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentReportOperationResult.Failure(message);
        }

        // 5. Kiểm tra `report.Version != command.Version` để chọn nhánh xử lý phù hợp.
        if (report.Version != command.Version)
        {
            // 6. Tính giá trị và lưu vào `message` để dùng ở bước tiếp theo.
            const string message = "Báo cáo đã thay đổi bởi yêu cầu khác. Vui lòng tải lại trước khi thao tác.";
            // 7. Gọi `RecordDismissDeniedAuditAsync` để thực hiện bước nghiệp vụ này.
            await RecordDismissDeniedAuditAsync(command, report, message, cancellationToken);
            // 8. Trả kết quả từ `Failure` cho nơi gọi.
            return ContentReportOperationResult.Failure(message);
        }

        // 9. Trả `null` cho nơi gọi.
        return null;
    }

    // Ghi audit riêng cho nhánh bị từ chối vì không có thay đổi nghiệp vụ để gộp transaction.
    private async Task RecordDismissDeniedAuditAsync(
        DismissContentReportCommand command,
        ContentReport report,
        string denialReason,
        CancellationToken cancellationToken)
    {
        // 1. Gọi `BuildAuditEntry` và lưu kết quả vào `entry`.
        AdminAuditEntry entry = BuildAuditEntry(
            command,
            AdminAuditOutcome.Denied,
            report,
            denialReason);
        // 2. Gọi `RecordAsync` để thực hiện bước nghiệp vụ này.
        await _auditService.RecordAsync(entry, cancellationToken);
    }

    // Tạo payload audit an toàn, chỉ gồm mã báo cáo, mã bộ và lý do nghiệp vụ.
    private static AdminAuditEntry BuildAuditEntry(
        DismissContentReportCommand command,
        string outcome,
        ContentReport report,
        string? denialReason)
    {
        // 1. Khởi tạo `metadata` với dữ liệu ban đầu cần thiết.
        var metadata = new Dictionary<string, string?>
        {
            ["FlashcardSetId"] = report.FlashcardSetId.ToString(),
            ["ReportReason"] = report.Reason,
            ["ReportStatus"] = report.Status,
            ["DeniedReason"] = denialReason
        };

        // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new AdminAuditEntry(
            ActorUserId: command.Actor.UserId,
            ActorDisplay: command.Actor.Display,
            Action: AdminAuditActions.ContentReportsDismiss,
            Outcome: outcome,
            TargetType: "ContentReport",
            TargetId: report.Id.ToString(),
            Reason: command.Reason,
            CorrelationId: command.Actor.CorrelationId,
            Metadata: metadata);
    }

    // Kiểm tra lệnh gửi báo cáo trước khi đụng database.
    private static string? ValidateSubmitCommand(SubmitContentReportCommand command)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(command.ReporterUserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.ReporterUserId))
        {
            // 2. Trả `"Vui lòng đăng nhập trước khi báo cáo nội dung."` cho nơi gọi.
            return "Vui lòng đăng nhập trước khi báo cáo nội dung.";
        }

        // 3. Gọi `NormalizeReason` và lưu kết quả vào `reason`.
        string reason = NormalizeReason(command.Reason);
        // 4. Kiểm tra `!IsValidReason(reason)` để chọn nhánh xử lý phù hợp.
        if (!IsValidReason(reason))
        {
            // 5. Trả `"Vui lòng chọn lý do báo cáo hợp lệ."` cho nơi gọi.
            return "Vui lòng chọn lý do báo cáo hợp lệ.";
        }

        // 6. Gọi `NormalizeOptional` và lưu kết quả vào `description`.
        string? description = NormalizeOptional(command.Description);
        // 7. Kiểm tra `description != null && description.Length > MaxDescriptionLength` để chọn nhánh xử lý phù hợp.
        if (description != null && description.Length > MaxDescriptionLength)
        {
            // 8. Trả `$"Mô tả không được vượt quá {MaxDescriptionLength} ký tự."` cho nơi gọi.
            return $"Mô tả không được vượt quá {MaxDescriptionLength} ký tự.";
        }

        // 9. Trả `null` cho nơi gọi.
        return null;
    }

    // Kiểm tra lệnh bác bỏ báo cáo trước khi ghi thay đổi nhạy cảm.
    private static string? ValidateDismissCommand(DismissContentReportCommand command)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(command.Actor.UserId)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.Actor.UserId))
        {
            // 2. Trả `"Không xác định được Quản trị viên đang thao tác."` cho nơi gọi.
            return "Không xác định được Quản trị viên đang thao tác.";
        }

        // 3. Kiểm tra `string.IsNullOrWhiteSpace(command.Reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            // 4. Trả `"Vui lòng nhập lý do xử lý trước khi bác bỏ báo cáo."` cho nơi gọi.
            return "Vui lòng nhập lý do xử lý trước khi bác bỏ báo cáo.";
        }

        // 5. Kiểm tra `command.Reason.Trim().Length > MaxResolutionReasonLength` để chọn nhánh xử lý phù hợp.
        if (command.Reason.Trim().Length > MaxResolutionReasonLength)
        {
            // 6. Trả `$"Lý do xử lý không được vượt quá {MaxResolutionReasonLength} ký tự."` cho nơi gọi.
            return $"Lý do xử lý không được vượt quá {MaxResolutionReasonLength} ký tự.";
        }

        // 7. Kiểm tra `command.Version <= 0` để chọn nhánh xử lý phù hợp.
        if (command.Version <= 0)
        {
            // 8. Trả `"Thiếu mã phiên bản báo cáo. Vui lòng tải lại trang."` cho nơi gọi.
            return "Thiếu mã phiên bản báo cáo. Vui lòng tải lại trang.";
        }

        // 9. Trả `null` cho nơi gọi.
        return null;
    }

    // Đưa reason về khóa ổn định để so sánh và lưu database.
    private static string NormalizeReason(string? reason)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(reason)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(reason))
        {
            // 2. Trả `string.Empty` cho nơi gọi.
            return string.Empty;
        }

        // 3. Trả kết quả từ `ToLowerInvariant` cho nơi gọi.
        return reason.Trim().ToLowerInvariant();
    }

    // Chuẩn hóa token lọc/sắp xếp nhập từ query string.
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

    // Cắt khoảng trắng và chuyển chuỗi rỗng thành null cho mô tả/lý do tùy chọn.
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

    // Kiểm tra reason có nằm trong danh mục cố định của phiên bản 1 hay không.
    private static bool IsValidReason(string reason)
    {
        // 1. Duyệt từng `option` trong `ReasonOptions` để xử lý lần lượt.
        foreach (ContentReportReasonOption option in ReasonOptions)
        {
            // 2. Kiểm tra `string.Equals(option.Value, reason, StringComparison.Ordinal)` để chọn nhánh xử lý phù hợp.
            if (string.Equals(option.Value, reason, StringComparison.Ordinal))
            {
                // 3. Trả `true` cho nơi gọi.
                return true;
            }
        }

        // 4. Trả `false` cho nơi gọi.
        return false;
    }

    // Đổi reason code sang nhãn tiếng Việt để service trả dữ liệu dễ dùng cho view.
    private static string ToReasonLabel(string reason)
    {
        // 1. Duyệt từng `option` trong `ReasonOptions` để xử lý lần lượt.
        foreach (ContentReportReasonOption option in ReasonOptions)
        {
            // 2. Kiểm tra `string.Equals(option.Value, reason, StringComparison.Ordinal)` để chọn nhánh xử lý phù hợp.
            if (string.Equals(option.Value, reason, StringComparison.Ordinal))
            {
                // 3. Trả `option.Label` cho nơi gọi.
                return option.Label;
            }
        }

        // 4. Trả `"Lý do khác"` cho nơi gọi.
        return "Lý do khác";
    }

    // Gỡ entity báo cáo vừa add khi unique index báo trùng để DbContext dùng tiếp được.
    private void DetachPendingContentReport(ContentReport report)
    {
        // 1. Gọi `Entry` và lưu kết quả vào `entry`.
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ContentReport> entry =
            _context.Entry(report);
        // 2. Kiểm tra `entry.State != EntityState.Detached` để chọn nhánh xử lý phù hợp.
        if (entry.State != EntityState.Detached)
        {
            // 3. Cập nhật `entry.State` bằng giá trị mới.
            entry.State = EntityState.Detached;
        }
    }

    // Gỡ toàn bộ entity tracked sau concurrency exception để tránh lưu lại trạng thái lỗi.
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
        int Version,
        int FlashcardSetVersion);
}
