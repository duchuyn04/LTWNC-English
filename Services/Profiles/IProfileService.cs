using ltwnc.Models.ViewModels.Profile;

namespace ltwnc.Services.Profiles;

// Hợp đồng đọc hồ sơ công khai và cập nhật thông tin cá nhân.
public interface IProfileService
{
    // Lấy hồ sơ theo username và quyền xem của người truy cập.
    Task<PublicProfileViewModel?> GetPublicProfileAsync(
        string username,
        string? viewerUserId,
        CancellationToken cancellationToken = default);

    // Lấy dữ liệu hiện tại để điền form chỉnh sửa hồ sơ.
    Task<ProfileEditViewModel> GetEditModelAsync(
        string userId,
        CancellationToken cancellationToken = default);

    // Kiểm tra và lưu thông tin hồ sơ người dùng.
    Task<ProfileOperationResult> UpdateProfileAsync(
        string userId,
        ProfileEditViewModel model,
        CancellationToken cancellationToken = default);

    // Xác minh mật khẩu cũ và thay bằng mật khẩu mới.
    Task<ProfileOperationResult> ChangePasswordAsync(
        string userId,
        ChangePasswordViewModel model,
        CancellationToken cancellationToken = default);
}
