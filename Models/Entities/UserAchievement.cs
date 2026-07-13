using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

// Huy hiệu user đã mở. Unique (UserId, Code) ở migration.
public class UserAchievement
{
    [Key]
    public int Id { get; set; }

    // AspNetUsers.Id
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    // Mã catalog, ví dụ first_card_mastered
    [Required]
    [MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    // Title copy lúc mở (UI không phụ thuộc catalog đổi)
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Mô tả copy lúc mở
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    // Thời điểm mở (UTC)
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}
