using ltwnc.Models.Entities;

namespace ltwnc.Models.ViewModels.Study;

// Safe transport model dùng cho View và POST settings; không chứa khóa database hoặc UserId.
public class StudySettingsViewModel
{
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

    public DictationContentMode DictationContentMode { get; set; } = DictationContentMode.Vocabulary;

    public DictationAnswerMode DictationAnswerMode { get; set; } = DictationAnswerMode.Term;

    public bool DictationAutoAdvance { get; set; }

    public float DictationPlaybackSpeed { get; set; } = 1.0f;

    public string? DictationVoiceUri { get; set; }

    public bool DictationShowHint { get; set; } = true;

    public bool DictationAcceptSynonyms { get; set; } = true;

    public bool DictationShuffle { get; set; }
}
