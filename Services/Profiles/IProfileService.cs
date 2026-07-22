using ltwnc.Models.ViewModels.Profile;

namespace ltwnc.Services.Profiles;

public interface IProfileService
{
    Task<PublicProfileViewModel?> GetPublicProfileAsync(
        string username,
        string? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<ProfileEditViewModel> GetEditModelAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> UpdateProfileAsync(
        string userId,
        ProfileEditViewModel model,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> ChangePasswordAsync(
        string userId,
        ChangePasswordViewModel model,
        CancellationToken cancellationToken = default);
}
