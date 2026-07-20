namespace ltwnc.Services.Profiles;

public sealed class AvatarUploadResult
{
    public bool Succeeded { get; init; }
    public string? AvatarPath { get; init; }
    public string? Error { get; init; }
}
