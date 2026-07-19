namespace ltwnc.Services.ContentReports;

public sealed record ContentReportReasonOption(string Value, string Label);

public sealed record SubmitContentReportCommand(
    int FlashcardSetId,
    string ReporterUserId,
    string Reason,
    string? Description);

public sealed record ContentReportSubmitResult(
    bool Succeeded,
    string Message,
    ContentReportSubmitFailure Failure = ContentReportSubmitFailure.None,
    long? ReportId = null)
{
    // Tạo kết quả thành công để controller chỉ lo redirect và thông báo UI.
    public static ContentReportSubmitResult Success(long reportId) =>
        new(true, "Đã gửi báo cáo nội dung. Cảm ơn bạn đã giúp giữ thư viện an toàn.", ContentReportSubmitFailure.None, reportId);

    // Tạo kết quả bị từ chối với lý do nghiệp vụ rõ ràng.
    public static ContentReportSubmitResult Rejected(string message, ContentReportSubmitFailure failure) =>
        new(false, message, failure);
}

public enum ContentReportSubmitFailure
{
    None,
    Validation,
    NotFoundOrPrivate,
    SelfReport,
    DuplicateOpenReport
}

public sealed record AdminContentReportQuery(
    string? Search = null,
    string? Status = null,
    string? Reason = null,
    string? Sort = null,
    int Page = ContentReportService.DefaultPage,
    int PageSize = ContentReportService.DefaultPageSize);

public sealed record AdminContentReportRow(
    long Id,
    int FlashcardSetId,
    string FlashcardSetTitle,
    string ReporterUserId,
    string ReporterDisplay,
    string OwnerUserId,
    string OwnerDisplay,
    string Reason,
    string ReasonLabel,
    string? Description,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionReason,
    int Version,
    int FlashcardSetVersion);

public sealed record AdminContentReportPage(
    IReadOnlyList<AdminContentReportRow> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages
    {
        get
        {
            if (TotalCount == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(TotalCount / (double)PageSize);
        }
    }
}

public sealed record DismissContentReportCommand(
    long ReportId,
    int Version,
    string ActorUserId,
    string ActorDisplay,
    string Reason,
    string? CorrelationId = null);

public sealed record ContentReportOperationResult(bool Succeeded, string Message)
{
    // Kết quả thành công dùng chung cho các thao tác xử lý báo cáo.
    public static ContentReportOperationResult Success(string message) => new(true, message);

    // Kết quả thất bại không ném exception để controller có thể hiển thị thông báo tiếng Việt.
    public static ContentReportOperationResult Failure(string message) => new(false, message);
}
