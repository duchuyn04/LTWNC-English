using System.ComponentModel.DataAnnotations;
using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Review;

// Hợp đồng input/output cho quick settings của Review.
// Không chứa Id database, UserId hoặc navigation property.
public sealed class ReviewSettingsViewModel
{
    [Range(ReviewSettingsPolicy.MinimumSessionSize, ReviewSettingsPolicy.MaximumSessionSize)]
    public int ReviewSessionSize { get; set; } = ReviewSettingsPolicy.DefaultSessionSize;

    [Range(ReviewSettingsPolicy.MinimumNewCardQuota, ReviewSettingsPolicy.MaximumNewCardQuota)]
    public int NewCardQuota { get; set; } = ReviewSettingsPolicy.DefaultNewCardQuota;

    [Range(ReviewSettingsPolicy.MinimumMaxIntervalDays, ReviewSettingsPolicy.MaximumMaxIntervalDays)]
    public int ReviewMaxIntervalDays { get; set; } = ReviewSettingsPolicy.DefaultMaxIntervalDays;

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
}
