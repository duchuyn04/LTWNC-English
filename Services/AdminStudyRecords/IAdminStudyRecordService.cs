namespace ltwnc.Services.AdminStudyRecords;

// Cổng truy vấn hồ sơ học tập ở chế độ chỉ đọc cho khu vực quản trị.
// Không có bất kỳ thao tác ghi nào: Admin không thể sửa điểm, sửa tiến độ
// hoặc xóa lịch sử học của ngườ học.
public interface IAdminStudyRecordService
{
    // Tìm, lọc, sắp xếp và phân trang phiên học phía máy chủ.
    Task<AdminStudySessionPage> SearchAsync(
        AdminStudySessionQuery query,
        CancellationToken cancellationToken = default);

    // Mở chi tiết một phiên học cấp ngườ học.
    // Bản ghi kiểm toán truy cập nhạy cảm được tạo TRƯỚC khi dữ liệu được trả về;
    // nếu ghi audit thất bại thì yêu cầu thất bại theo (thất bại an toàn).
    // Trả về null khi phiên không tồn tại.
    Task<AdminStudySessionDetails?> GetDetailsAsync(
        int sessionId,
        AdminStudyRecordAccessCommand access,
        CancellationToken cancellationToken = default);
}
