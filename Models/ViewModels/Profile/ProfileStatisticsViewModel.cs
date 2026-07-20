namespace ltwnc.Models.ViewModels.Profile;

public sealed class ProfileStatisticsViewModel
{
    public int OwnedSetCount { get; init; }
    public int PublicSetCount { get; init; }
    public int TotalFlashcardCount { get; init; }
    public int LearnedFlashcardCount { get; init; }
    public int CompletedSessionCount { get; init; }
    public int UnlockedBadgeCount { get; init; }
    public int CurrentStreak { get; init; }
}
