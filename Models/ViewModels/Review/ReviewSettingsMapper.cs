using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Review;

public static class ReviewSettingsMapper
{
    public static ReviewSettingsViewModel ToViewModel(ReviewSettings settings) => new()
    {
        ReviewSessionSize = settings.ReviewSessionSize,
        NewCardQuota = settings.NewCardQuota,
        ReviewMaxIntervalDays = settings.ReviewMaxIntervalDays,
        ShowFrontTerm = settings.ShowFrontTerm,
        ShowFrontDefinition = settings.ShowFrontDefinition,
        ShowFrontIpa = settings.ShowFrontIpa,
        ShowFrontImage = settings.ShowFrontImage,
        ShowBackTerm = settings.ShowBackTerm,
        ShowBackDefinition = settings.ShowBackDefinition,
        ShowBackIpa = settings.ShowBackIpa,
        ShowBackExample = settings.ShowBackExample,
        ShowBackImage = settings.ShowBackImage,
        HideImage = settings.HideImage,
        BlurImage = settings.BlurImage,
        LargeImage = settings.LargeImage,
        PronounceFront = settings.PronounceFront,
        PronounceBack = settings.PronounceBack
    };

    public static ReviewSettings ToEntity(
        string userId,
        int flashcardSetId,
        ReviewSettingsViewModel input)
    {
        ReviewSettings entity = ReviewSettings.CreateDefault(
            userId,
            flashcardSetId,
            input.NewCardQuota);
        Apply(entity, input);
        return entity;
    }

    public static void Apply(ReviewSettings entity, ReviewSettingsViewModel input)
    {
        entity.ReviewSessionSize = input.ReviewSessionSize;
        entity.NewCardQuota = input.NewCardQuota;
        entity.ReviewMaxIntervalDays = input.ReviewMaxIntervalDays;
        entity.ShowFrontTerm = input.ShowFrontTerm;
        entity.ShowFrontDefinition = input.ShowFrontDefinition;
        entity.ShowFrontIpa = input.ShowFrontIpa;
        entity.ShowFrontImage = input.ShowFrontImage;
        entity.ShowBackTerm = input.ShowBackTerm;
        entity.ShowBackDefinition = input.ShowBackDefinition;
        entity.ShowBackIpa = input.ShowBackIpa;
        entity.ShowBackExample = input.ShowBackExample;
        entity.ShowBackImage = input.ShowBackImage;
        entity.HideImage = input.HideImage;
        entity.BlurImage = input.BlurImage;
        entity.LargeImage = input.LargeImage;
        entity.PronounceFront = input.PronounceFront;
        entity.PronounceBack = input.PronounceBack;
    }
}
