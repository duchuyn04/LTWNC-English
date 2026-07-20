namespace ltwnc.Models.ViewModels.Leaderboard;

public sealed class LeaderboardPageViewModel
{
    public int PeriodDays { get; init; }
    public IReadOnlyList<LeaderboardEntryViewModel> Entries { get; init; } = [];
    public LeaderboardEntryViewModel? ViewerEntry { get; init; }
    public string EmptyMessage { get; init; } = "Chưa có dữ liệu xếp hạng trong kỳ này.";
    public bool IsEmpty => Entries.Count == 0;
}
