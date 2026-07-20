namespace ltwnc.Models.ViewModels.Leaderboard;

public sealed class LeaderboardEntryViewModel
{
    public int Rank { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? AvatarPath { get; init; }
    public string AvatarInitial { get; init; } = string.Empty;
    public long TotalSeconds { get; init; }
    public int SessionCount { get; init; }
    public bool IsViewer { get; init; }
}
