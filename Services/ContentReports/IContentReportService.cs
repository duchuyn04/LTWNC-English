namespace ltwnc.Services.ContentReports;

// Hợp đồng tiếp nhận và xử lý báo cáo nội dung từ người dùng, Admin.
public interface IContentReportService
{
    // Trả danh sách lý do báo cáo hợp lệ cho giao diện.
    IReadOnlyList<ContentReportReasonOption> GetReasonOptions();

    // Kiểm tra người dùng đã có báo cáo đang chờ cho bộ thẻ hay chưa.
    Task<bool> HasOpenReportAsync(
        int flashcardSetId,
        string reporterUserId,
        CancellationToken cancellationToken = default);

    // Tạo báo cáo mới sau khi kiểm tra quyền và dữ liệu đầu vào.
    Task<ContentReportSubmitResult> SubmitAsync(
        SubmitContentReportCommand command,
        CancellationToken cancellationToken = default);

    // Tìm kiếm và phân trang hàng đợi báo cáo cho Admin.
    Task<AdminContentReportPage> SearchForAdminAsync(
        AdminContentReportQuery query,
        CancellationToken cancellationToken = default);

    // Đếm báo cáo chờ xử lý lâu hơn khoảng thời gian được chỉ định.
    Task<int> CountPendingOlderThanAsync(
        TimeSpan age,
        CancellationToken cancellationToken = default);

    // Bác bỏ một báo cáo và ghi lại lý do xử lý.
    Task<ContentReportOperationResult> DismissAsync(
        DismissContentReportCommand command,
        CancellationToken cancellationToken = default);
}
