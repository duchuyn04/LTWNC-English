using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

// Entity đại diện cho bảng CardActionLogs — lịch sử các hành động hàng loạt trên thẻ
// Dùng để hỗ trợ tính năng hoàn tác (Undo)
public class CardActionLog
{
    // Khóa chính, tự động tăng
    [Key]
    public int Id { get; set; }

    // Id của ngườidùng thực hiện hành động
    public string UserId { get; set; } = string.Empty;

    // Id của bộ thẻ bị tác động
    public int SetId { get; set; }

    // Loại hành động: Delete, Star, Unstar
    public string ActionType { get; set; } = string.Empty;

    // Danh sách id thẻ bị tác động, lưu dưới dạng JSON
    public string CardIdsJson { get; set; } = string.Empty;

    // Snapshot trạng thái trước khi thay đổi, dùng để hoàn tác
    public string SnapshotJson { get; set; } = string.Empty;

    // Thờidạm thực hiện hành động
    public DateTime ExecutedAt { get; set; }

    // Thờidạm hoàn tác (null nếu chưa hoàn tác)
    public DateTime? UndoneAt { get; set; }
}
