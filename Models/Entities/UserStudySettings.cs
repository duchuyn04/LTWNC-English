using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ltwnc.Models.Entities;

public class UserStudySettings
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool StarredOnly { get; set; }
    public bool UnlearnedOnly { get; set; }

    public bool ShowFrontTerm { get; set; } = true;
    public bool ShowFrontDefinition { get; set; }
    public bool ShowFrontIpa { get; set; } = true;
    public bool ShowFrontImage { get; set; }

    public bool ShowBackTerm { get; set; }
    public bool ShowBackDefinition { get; set; } = true;
    public bool ShowBackIpa { get; set; }
    public bool ShowBackExample { get; set; } = true;
    public bool ShowBackImage { get; set; } = true;

    public bool HideImage { get; set; }
    public bool BlurImage { get; set; }
    public bool LargeImage { get; set; }

    public bool PronounceFront { get; set; } = true;
    public bool PronounceBack { get; set; }

    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }
}
