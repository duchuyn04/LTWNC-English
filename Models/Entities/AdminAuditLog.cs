using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

// Bản ghi kiểm toán quản trị — chỉ ghi thêm, không sửa/xóa thủ công.
public class AdminAuditLog
{
    [Key]
    public long Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    [Required, MaxLength(450)]
    public string ActorUserId { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string ActorDisplay { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TargetType { get; set; }

    [MaxLength(100)]
    public string? TargetId { get; set; }

    [Required, MaxLength(40)]
    public string Outcome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }

    [MaxLength(64)]
    public string? CorrelationId { get; set; }

    // Metadata đã qua lọc danh sách trường cho phép, dạng JSON an toàn.
    [MaxLength(2000)]
    public string? MetadataJson { get; set; }
}
