using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace ltwnc.Services.Profiles;

public sealed class AvatarService : IAvatarService
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const string AvatarUrlPrefix = "/uploads/avatars/";

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;

    public AvatarService(
        AppDbContext db,
        IWebHostEnvironment environment,
        TimeProvider timeProvider)
    {
        _db = db;
        _environment = environment;
        _timeProvider = timeProvider;
    }

    public async Task<AvatarUploadResult> ReplaceAvatarAsync(
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return Failure("Vui lòng chọn ảnh đại diện.");
        }

        if (file.Length > MaxFileSize)
        {
            return Failure("Ảnh đại diện không được vượt quá 5 MB.");
        }

        await using Stream input = file.OpenReadStream();
        IImageFormat? format;
        Image image;
        try
        {
            format = await Image.DetectFormatAsync(input, cancellationToken);
            if (format == null || !IsAllowedFormat(format))
            {
                return Failure("Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.");
            }

            input.Position = 0;
            image = await Image.LoadAsync(input, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            return Failure("File ảnh không hợp lệ.");
        }
        catch (InvalidImageContentException)
        {
            return Failure("File ảnh không hợp lệ.");
        }

        using (image)
        {
            if (image.Width != image.Height)
            {
                return Failure("Ảnh sau khi crop phải có tỷ lệ vuông.");
            }

            UserProfile? profile = await _db.UserProfiles
                .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (profile == null)
            {
                DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
                profile = new UserProfile { UserId = userId, CreatedAt = now };
                _db.UserProfiles.Add(profile);
            }

            string directory = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(directory);
            string fileName = $"{Guid.NewGuid():N}.png";
            string physicalPath = Path.Combine(directory, fileName);
            string avatarPath = AvatarUrlPrefix + fileName;

            await image.SaveAsPngAsync(physicalPath, cancellationToken);

            string? oldAvatarPath = profile.AvatarPath;
            profile.AvatarPath = avatarPath;
            profile.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                File.Delete(physicalPath);
                throw;
            }

            DeleteOldAvatar(oldAvatarPath);
            return new AvatarUploadResult
            {
                Succeeded = true,
                AvatarPath = avatarPath
            };
        }
    }

    private static bool IsAllowedFormat(IImageFormat format) =>
        format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase) ||
        format.Name.Equals("PNG", StringComparison.OrdinalIgnoreCase) ||
        format.Name.Equals("WEBP", StringComparison.OrdinalIgnoreCase);

    private void DeleteOldAvatar(string? avatarPath)
    {
        if (string.IsNullOrWhiteSpace(avatarPath) ||
            !avatarPath.StartsWith(AvatarUrlPrefix, StringComparison.Ordinal))
        {
            return;
        }

        string fileName = Path.GetFileName(avatarPath);
        string physicalPath = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "avatars",
            fileName);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }

    private static AvatarUploadResult Failure(string error) => new() { Error = error };
}
