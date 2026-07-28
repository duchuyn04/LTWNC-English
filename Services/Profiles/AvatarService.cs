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
    private const int MaxDimension = 4096;
    private const string AvatarUrlPrefix = "/uploads/avatars/";

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;

    public AvatarService(
        AppDbContext db,
        IWebHostEnvironment environment,
        TimeProvider timeProvider)
    {
        // 1. Lưu dependency `_db` để các phương thức khác sử dụng.
        _db = db;
        // 2. Lưu dependency `_environment` để các phương thức khác sử dụng.
        _environment = environment;
        // 3. Lưu dependency `_timeProvider` để các phương thức khác sử dụng.
        _timeProvider = timeProvider;
    }

    public async Task<AvatarUploadResult> ReplaceAvatarAsync(
        string userId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra `file == null || file.Length == 0` để chọn nhánh xử lý phù hợp.
        if (file == null || file.Length == 0)
        {
            // 2. Trả kết quả từ `Failure` cho nơi gọi.
            return Failure("Vui lòng chọn ảnh đại diện.");
        }

        // 3. Kiểm tra `file.Length > MaxFileSize` để chọn nhánh xử lý phù hợp.
        if (file.Length > MaxFileSize)
        {
            // 4. Trả kết quả từ `Failure` cho nơi gọi.
            return Failure("Ảnh đại diện không được vượt quá 5 MB.");
        }

        // 5. Gọi `OpenReadStream` và lưu kết quả vào `input`.
        await using Stream input = file.OpenReadStream();
        // 6. Khai báo `format` để lưu dữ liệu dùng ở các bước sau.
        IImageFormat? format;
        // 7. Khai báo `image` để lưu dữ liệu dùng ở các bước sau.
        Image image;
        // 8. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 9. Cập nhật `format` bằng giá trị mới.
            format = await Image.DetectFormatAsync(input, cancellationToken);
            // 10. Kiểm tra `format == null || !IsAllowedFormat(format)` để chọn nhánh xử lý phù hợp.
            if (format == null || !IsAllowedFormat(format))
            {
                // 11. Trả kết quả từ `Failure` cho nơi gọi.
                return Failure("Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.");
            }

            // 12. Cập nhật `input.Position` bằng giá trị mới.
            input.Position = 0;
            // 13. Gọi `IdentifyAsync` và lưu kết quả vào `imageInfo`.
            ImageInfo imageInfo = await Image.IdentifyAsync(input, cancellationToken);
            // 14. Kiểm tra `imageInfo.Width > MaxDimension || imageInfo.Height > MaxDimension` để chọn nhánh xử lý phù hợp.
            if (imageInfo.Width > MaxDimension || imageInfo.Height > MaxDimension)
            {
                // 15. Trả kết quả từ `Failure` cho nơi gọi.
                return Failure("Kích thước ảnh không được vượt quá 4096 x 4096 pixel.");
            }

            // 16. Cập nhật `input.Position` bằng giá trị mới.
            input.Position = 0;
            // 17. Cập nhật `image` bằng giá trị mới.
            image = await Image.LoadAsync(input, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            // 18. Trả kết quả từ `Failure` cho nơi gọi.
            return Failure("File ảnh không hợp lệ.");
        }
        catch (InvalidImageContentException)
        {
            // 19. Trả kết quả từ `Failure` cho nơi gọi.
            return Failure("File ảnh không hợp lệ.");
        }

        // 20. Mở tài nguyên dùng tạm và tự động giải phóng sau khi xử lý.
        using (image)
        {
            // 21. Kiểm tra `image.Width != image.Height` để chọn nhánh xử lý phù hợp.
            if (image.Width != image.Height)
            {
                // 22. Trả kết quả từ `Failure` cho nơi gọi.
                return Failure("Ảnh sau khi crop phải có tỷ lệ vuông.");
            }

            // 23. Gọi `SingleOrDefaultAsync` và lưu kết quả vào `profile`.
            UserProfile? profile = await _db.UserProfiles
                .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            // 24. Kiểm tra `profile == null` để chọn nhánh xử lý phù hợp.
            if (profile == null)
            {
                // 25. Tính giá trị và lưu vào `now` để dùng ở bước tiếp theo.
                DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
                // 26. Cập nhật `profile` bằng giá trị mới.
                profile = new UserProfile { UserId = userId, CreatedAt = now };
                // 27. Gọi `Add` để thực hiện bước nghiệp vụ này.
                _db.UserProfiles.Add(profile);
            }

            // 28. Gọi `Combine` và lưu kết quả vào `directory`.
            string directory = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
            // 29. Gọi `CreateDirectory` để thực hiện bước nghiệp vụ này.
            Directory.CreateDirectory(directory);
            // 30. Tính giá trị và lưu vào `fileName` để dùng ở bước tiếp theo.
            string fileName = $"{Guid.NewGuid():N}.png";
            // 31. Gọi `Combine` và lưu kết quả vào `physicalPath`.
            string physicalPath = Path.Combine(directory, fileName);
            // 32. Tính giá trị và lưu vào `avatarPath` để dùng ở bước tiếp theo.
            string avatarPath = AvatarUrlPrefix + fileName;

            // 33. Gọi `SaveAsPngAsync` để thực hiện bước nghiệp vụ này.
            await image.SaveAsPngAsync(physicalPath, cancellationToken);

            // 34. Tính giá trị và lưu vào `oldAvatarPath` để dùng ở bước tiếp theo.
            string? oldAvatarPath = profile.AvatarPath;
            // 35. Cập nhật `profile.AvatarPath` bằng giá trị mới.
            profile.AvatarPath = avatarPath;
            // 36. Cập nhật `profile.UpdatedAt` bằng giá trị mới.
            profile.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            // 37. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
            try
            {
                // 38. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // 39. Gọi `Delete` để thực hiện bước nghiệp vụ này.
                File.Delete(physicalPath);
                // 40. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
                throw;
            }

            // 41. Gọi `DeleteOldAvatar` để thực hiện bước nghiệp vụ này.
            DeleteOldAvatar(oldAvatarPath);
            // 42. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AvatarUploadResult
            {
                Succeeded = true,
                AvatarPath = avatarPath
            };
        }
    }

    private static bool IsAllowedFormat(IImageFormat format)
    {
        // 1. Trả `format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase) || f...` cho nơi gọi.
        return format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase) ||
        format.Name.Equals("PNG", StringComparison.OrdinalIgnoreCase) ||
        format.Name.Equals("WEBP", StringComparison.OrdinalIgnoreCase);
    }

    private void DeleteOldAvatar(string? avatarPath)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(avatarPath) || !avatarPath.StartsWith(Ava...` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(avatarPath) ||
            !avatarPath.StartsWith(AvatarUrlPrefix, StringComparison.Ordinal))
        {
            // 2. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 3. Gọi `GetFileName` và lưu kết quả vào `fileName`.
        string fileName = Path.GetFileName(avatarPath);
        // 4. Gọi `Combine` và lưu kết quả vào `physicalPath`.
        string physicalPath = Path.Combine(
            _environment.WebRootPath,
            "uploads",
            "avatars",
            fileName);
        // 5. Kiểm tra `File.Exists(physicalPath)` để chọn nhánh xử lý phù hợp.
        if (File.Exists(physicalPath))
        {
            // 6. Gọi `Delete` để thực hiện bước nghiệp vụ này.
            File.Delete(physicalPath);
        }
    }

    private static AvatarUploadResult Failure(string error)
    {
        // 1. Tạo và trả đối tượng kết quả cho nơi gọi.
        return new() { Error = error };
    }
}
