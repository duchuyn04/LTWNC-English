namespace ltwnc.Models.ViewModels.Profile;

public sealed class PublicProfileViewModel
{
    public string Username { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? AvatarPath { get; init; }
    public string AvatarInitial { get; init; } = string.Empty;
    public bool IsOwner { get; init; }
    public bool IsPrivate { get; init; }
    public bool ShowStats { get; init; }
    public bool ShowBadges { get; init; }
    public bool ShowActivity { get; init; }
    public bool ShowPublicSets { get; init; }
    public ProfileStatisticsViewModel? Statistics { get; init; }
    public IReadOnlyList<ProfileTimelineItemViewModel> Timeline { get; init; } = [];
    public IReadOnlyList<ProfileBadgeViewModel> Badges { get; init; } = [];
    public IReadOnlyList<ProfilePublicSetViewModel> PublicSets { get; init; } = [];
}
