namespace ltwnc.Models.ViewModels.Profile;

public sealed class ProfilePublicSetViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CardCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
