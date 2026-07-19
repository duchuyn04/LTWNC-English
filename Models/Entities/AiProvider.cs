using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class AiProvider
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string AdapterType { get; set; } = "OpenAICompatible";

    [Required, MaxLength(500)]
    public string BaseUrl { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ModelId { get; set; } = string.Empty;

    // Khóa bí mật chỉ được lưu ở dạng đã mã hóa; không bao giờ trả khóa gốc ra ngoài.
    public string? EncryptedApiKey { get; set; }

    // Chỉ giữ bốn ký tự cuối để giao diện hiển thị trạng thái khóa.
    [MaxLength(4)]
    public string? ApiKeyLastFour { get; set; }

    public bool IsEnabled { get; set; } = true;

    // Đánh dấu nhà cung cấp chính; toàn hệ thống chỉ có tối đa một nhà cung cấp chính.
    public bool IsPrimary { get; set; }

    public int Priority { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public DateTime? LastCheckedAt { get; set; }
    public bool? LastCheckSucceeded { get; set; }
    public string? LastError { get; set; }

    // Số lần kiểm tra kết nối thất bại liên tiếp; lần thành công sẽ reset về 0.
    public int ConsecutiveFailureCount { get; set; }

    // Khóa phiên bản lạc quan: mỗi lần ghi thay đổi đều tăng lên một
    // để phát hiện hai quản trị viên sửa cùng một bản ghi.
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
