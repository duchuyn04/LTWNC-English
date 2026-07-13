using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

// Lịch sử action batch trên thẻ; snapshot để Undo.
public class CardActionLog
{
    [Key]
    public int Id { get; set; }

    // User đã chạy action
    public string UserId { get; set; } = string.Empty;

    // Bộ thẻ bị tác động
    public int SetId { get; set; }

    // "Delete" | "Star" | "Unstar" (khớp factory)
    public string ActionType { get; set; } = string.Empty;

    // JSON mảng id thẻ
    public string CardIdsJson { get; set; } = string.Empty;

    // JSON snapshot trạng thái trước khi đổi (command serialize)
    public string SnapshotJson { get; set; } = string.Empty;

    // Lúc Execute
    public DateTime ExecutedAt { get; set; }

    // Lúc Undo; null = chưa hoàn tác
    public DateTime? UndoneAt { get; set; }
}
