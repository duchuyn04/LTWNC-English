using Microsoft.AspNetCore.Http;

namespace ltwnc.Services.Profiles;

public interface IAvatarService
{
    Task<AvatarUploadResult> ReplaceAvatarAsync(
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default);
}
