using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

// Tài khoản ứng dụng (bảng Users) — thay ASP.NET Identity.
public class AppUser
{
    [Key]
    [MaxLength(450)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    // Chuỗi hash versioned do Pbkdf2PasswordHasher tạo
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
