using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.Entities;

public class UserProfile
{
    [Key]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Bio { get; set; }

    public string? AvatarPath { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool ShowStats { get; set; }
    public bool ShowBadges { get; set; }
    public bool ShowActivity { get; set; }
    public bool ShowPublicSets { get; set; }
    public DateTime? LastUsernameChangedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
