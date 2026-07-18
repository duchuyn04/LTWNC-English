namespace ltwnc.Models.ViewModels.Profile;

public sealed class ProfileBadgeViewModel
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime UnlockedAt { get; init; }
}
