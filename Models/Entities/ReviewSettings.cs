using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ltwnc.Models.Entities;

// Cấu hình ôn tập riêng cho một user và một bộ thẻ.
// Không dùng chung với UserStudySettings để thay đổi Flashcard/Dictation
// không thể vô tình làm thay đổi lịch ôn.
public sealed class ReviewSettings
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int FlashcardSetId { get; set; }

    public int ReviewSessionSize { get; set; } = ReviewSettingsPolicy.DefaultSessionSize;

    public int NewCardQuota { get; set; } = ReviewSettingsPolicy.DefaultNewCardQuota;

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

    [ForeignKey(nameof(FlashcardSetId))]
    public FlashcardSet? FlashcardSet { get; set; }

    public static ReviewSettings CreateDefault(string userId, int flashcardSetId, int newCardQuota)
    {
        int safeQuota = IsValidNewCardQuota(newCardQuota)
            ? newCardQuota
            : ReviewSettingsPolicy.DefaultNewCardQuota;

        return new ReviewSettings
        {
            UserId = userId,
            FlashcardSetId = flashcardSetId,
            NewCardQuota = safeQuota
        };
    }

    public static bool IsValidNewCardQuota(int value) =>
        value >= ReviewSettingsPolicy.MinimumNewCardQuota
        && value <= ReviewSettingsPolicy.MaximumNewCardQuota;

    public static ReviewSettings CreateFromLegacy(
        string userId,
        int flashcardSetId,
        int legacyNewCardQuota,
        UserStudySettings? legacy)
    {
        ReviewSettings settings = CreateDefault(userId, flashcardSetId, legacyNewCardQuota);
        if (legacy == null)
        {
            return settings;
        }

        settings.ReviewSessionSize = IsValidSessionSize(legacy.ReviewSessionSize)
            ? legacy.ReviewSessionSize
            : ReviewSettingsPolicy.DefaultSessionSize;
        settings.ReviewMaxIntervalDays = IsValidMaxIntervalDays(legacy.ReviewMaxIntervalDays)
            ? legacy.ReviewMaxIntervalDays
            : ReviewSettingsPolicy.DefaultMaxIntervalDays;
        settings.ShowFrontTerm = legacy.ShowFrontTerm;
        settings.ShowFrontDefinition = legacy.ShowFrontDefinition;
        settings.ShowFrontIpa = legacy.ShowFrontIpa;
        settings.ShowFrontImage = legacy.ShowFrontImage;
        settings.ShowBackTerm = legacy.ShowBackTerm;
        settings.ShowBackDefinition = legacy.ShowBackDefinition;
        settings.ShowBackIpa = legacy.ShowBackIpa;
        settings.ShowBackExample = legacy.ShowBackExample;
        settings.ShowBackImage = legacy.ShowBackImage;
        settings.HideImage = legacy.HideImage;
        settings.BlurImage = legacy.BlurImage;
        settings.LargeImage = legacy.LargeImage;
        settings.PronounceFront = legacy.PronounceFront;
        settings.PronounceBack = legacy.PronounceBack;
        return settings;
    }

    private static bool IsValidSessionSize(int value) =>
        value >= ReviewSettingsPolicy.MinimumSessionSize
        && value <= ReviewSettingsPolicy.MaximumSessionSize;

    private static bool IsValidMaxIntervalDays(int value) =>
        value >= ReviewSettingsPolicy.MinimumMaxIntervalDays
        && value <= ReviewSettingsPolicy.MaximumMaxIntervalDays;
}
