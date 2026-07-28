using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.Profiles;

// Hợp đồng kiểm tra, lưu và thay thế ảnh đại diện người dùng.
public interface IAvatarService
{
    // Thay ảnh hiện tại bằng tệp mới và trả đường dẫn hoặc lỗi.
    Task<AvatarUploadResult> ReplaceAvatarAsync(
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default);
}
