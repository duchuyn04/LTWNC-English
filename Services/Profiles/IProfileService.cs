using ltwnc.Models.ViewModels.Profile;

namespace ltwnc.Services.Profiles;

public interface IProfileService
{
    Task<PublicProfileViewModel?> GetPublicProfileAsync(
        string username,
        string? viewerUserId,
        CancellationToken cancellationToken = default);
}
