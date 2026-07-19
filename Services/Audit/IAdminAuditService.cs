using ltwnc.Models.Entities;

namespace ltwnc.Services.Audit;

// Cổng dùng chung cho mọi thao tác quản trị và truy cập dữ liệu nhạy cảm.
// Chỉ hỗ trợ ghi thêm và tra cứu; không có thao tác sửa/xóa thủ công.
// Truy cập nhạy cảm phải await RecordAsync thành công trước khi trả dữ liệu;
// thất bại kiểm toán phải làm yêu cầu thất bại theo.
public interface IAdminAuditService
{
    // Ghi bản ghi trong giao dịch của ngữ cảnh hiện tại mà không tự lưu,
    // để caller kết hợp cùng thay đổi nghiệp vụ trong một giao dịch.
    void Enqueue(AdminAuditEntry entry);

    // Ghi bản ghi và lưu ngay. Ném lỗi nếu không ghi được (thất bại an toàn).
    Task<AdminAuditLog> RecordAsync(
        AdminAuditEntry entry,
        CancellationToken cancellationToken = default);

    Task<AdminAuditLogPage> SearchAsync(
        AdminAuditQuery query,
        CancellationToken cancellationToken = default);
}
