using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

// ============================================================
// Bảng "thành tích đã mở khóa" của từng người dùng.
// Mỗi dòng = một huy hiệu user đã nhận được.
// Ví dụ: user A đã mở "Thẻ đầu tiên đã thuộc" vào lúc 10:00.
// Không lưu hai lần cùng một mã cho cùng một user (index unique).
// ============================================================
public class UserAchievement
{
    // Khóa chính, tự tăng
    [Key]
    public int Id { get; set; }

    // Ai nhận thành tích này (id trong bảng đăng nhập AspNetUsers)
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    // Mã cố định của huy hiệu, ví dụ first_card_mastered
    [Required]
    [MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    // Tên hiển thị cho user (copy từ danh mục lúc mở khóa)
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Mô tả ngắn, giải thích vì sao được huy hiệu
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    // Thời điểm mở khóa (giờ UTC)
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}
