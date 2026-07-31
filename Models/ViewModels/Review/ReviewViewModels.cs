using ltwnc.Models.Entities;

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

    public IReadOnlyList<ReviewRatingPreviewViewModel> RatingPreviews { get; set; } = Array.Empty<ReviewRatingPreviewViewModel>();

    public bool IsNewCard { get; set; }

    public bool IsRated { get; set; }
}

public sealed class ReviewRatingPreviewViewModel
{
    public ReviewRating Rating { get; set; }

    public DateTimeOffset NextReviewAtUtc { get; set; }

    public int LongTermIntervalDays { get; set; }

    public TimeSpan Delay { get; set; }

    public string DelayLabel { get; set; } = string.Empty;
}

public sealed class ReviewSessionViewModel
{
    public int SessionId { get; set; }

    public int? SetId { get; set; }

    public string SetTitle { get; set; } = string.Empty;

    public int TotalCards { get; set; }

    public int RatedCards { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsEnded { get; set; }

    public bool IsFinished => IsCompleted || IsEnded;

    public ReviewSettingsViewModel Settings { get; set; } = new();

    public IReadOnlyList<ReviewCardViewModel> Cards { get; set; } = Array.Empty<ReviewCardViewModel>();
}

public sealed class ReviewSetViewModel
{
    public int SetId { get; set; }

    public string SetTitle { get; set; } = string.Empty;

    public int TotalCards { get; set; }

    public int DueCards { get; set; }

    public int NewCards { get; set; }

    public bool IsPaused { get; set; }

    public ReviewSettingsViewModel Settings { get; set; } = new();
}

public sealed class ReviewSettingsPanelViewModel
{
    public int SetId { get; set; }

    public ReviewSettingsViewModel Settings { get; set; } = new();
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
