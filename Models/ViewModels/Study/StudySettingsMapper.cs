using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Mapping tại biên MVC giữa entity persistence và request/response contracts.
public static class StudySettingsMapper
{
    public static StudySettingsViewModel ToViewModel(UserStudySettings settings)
    {
        return new StudySettingsViewModel
        {
            StarredOnly = settings.StarredOnly,
            UnlearnedOnly = settings.UnlearnedOnly,
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
            PronounceBack = settings.PronounceBack,
            DictationContentMode = settings.DictationContentMode,
            DictationAnswerMode = settings.DictationAnswerMode,
            DictationAutoAdvance = settings.DictationAutoAdvance,
            DictationPlaybackSpeed = settings.DictationPlaybackSpeed,
            DictationVoiceUri = settings.DictationVoiceUri,
            DictationShowHint = settings.DictationShowHint,
            DictationAcceptSynonyms = settings.DictationAcceptSynonyms,
            DictationShuffle = settings.DictationShuffle
        };
    }

    public static UserStudySettings ToEntity(StudySettingsViewModel settings)
    {
        return new UserStudySettings
        {
            StarredOnly = settings.StarredOnly,
            UnlearnedOnly = settings.UnlearnedOnly,
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
            PronounceBack = settings.PronounceBack,
            DictationContentMode = settings.DictationContentMode,
            DictationAnswerMode = settings.DictationAnswerMode,
            DictationAutoAdvance = settings.DictationAutoAdvance,
            DictationPlaybackSpeed = settings.DictationPlaybackSpeed,
            DictationVoiceUri = settings.DictationVoiceUri,
            DictationShowHint = settings.DictationShowHint,
            DictationAcceptSynonyms = settings.DictationAcceptSynonyms,
            DictationShuffle = settings.DictationShuffle
        };
    }
}
