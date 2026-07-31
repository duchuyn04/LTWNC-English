using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.Study;

namespace ltwnc.Models.ViewModels.Review;

public sealed class ReviewCardViewModel
{
    public int FlashcardId { get; set; }

    public string SetTitle { get; set; } = string.Empty;

    public string FrontText { get; set; } = string.Empty;

    public string BackText { get; set; } = string.Empty;

    public string Pronunciation { get; set; } = string.Empty;

    public string ExampleSentence { get; set; } = string.Empty;

    public string ExampleMeaning { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? UploadedImagePath { get; set; }

    public ReviewStage Stage { get; set; }

    public ReviewRating? Rating { get; set; }

    public bool IsNewCard { get; set; }

    public bool IsRated { get; set; }
}

public sealed class ReviewSessionViewModel
{
    public int SessionId { get; set; }

    public int TotalCards { get; set; }

    public int RatedCards { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsEnded { get; set; }

    public bool IsFinished => IsCompleted || IsEnded;

    public StudySettingsViewModel Settings { get; set; } = new();

    public IReadOnlyList<ReviewCardViewModel> Cards { get; set; } = Array.Empty<ReviewCardViewModel>();
}

public sealed class ReviewProgressViewModel
{
    public ReviewStage Stage { get; set; }

    public DateTimeOffset NextReviewAtUtc { get; set; }

    public int LongTermIntervalDays { get; set; }

    public DateTimeOffset LastRatedAtUtc { get; set; }
}

public sealed class ReviewRatingResult
{
    public ReviewSessionViewModel Session { get; init; } = new();

    public ReviewProgressViewModel Progress { get; init; } = new();
}
