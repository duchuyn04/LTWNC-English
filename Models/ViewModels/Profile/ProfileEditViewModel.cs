using System.ComponentModel.DataAnnotations;

namespace ltwnc.Models.ViewModels.Profile;

public sealed class ProfileEditViewModel
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Bio { get; set; }

    public string Email { get; set; } = string.Empty;
    public string? AvatarPath { get; init; }
    public string AvatarInitial { get; init; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool ShowStats { get; set; }
    public bool ShowBadges { get; set; }
    public bool ShowActivity { get; set; }
    public bool ShowPublicSets { get; set; }
}
