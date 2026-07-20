namespace ltwnc.Models.ViewModels.Profile;

public sealed class ProfileTimelineItemViewModel
{
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public DateTime Timestamp { get; init; }
}
