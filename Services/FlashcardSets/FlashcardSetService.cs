using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace ltwnc.Services.FlashcardSets;

// CRUD bộ thẻ / thẻ, copy public set, upload ảnh.
// Sửa/xóa chỉ chủ sở hữu.
public class FlashcardSetService : IFlashcardSetService
{
    private const string UploadedImageUrlPrefix = "/uploads/flashcards/";
    // FlashcardSets, Flashcards, progress liên quan
    private readonly AppDbContext _context;

    // WebRootPath cho thư mục uploads/flashcards
    private readonly IWebHostEnvironment _environment;

    // Inject DbContext và hosting (đường dẫn upload)
    public FlashcardSetService(AppDbContext context, IWebHostEnvironment environment)
    {
        // 1. Lưu dependency `_context` để các phương thức khác sử dụng.
        _context = context;
        // 2. Lưu dependency `_environment` để các phương thức khác sử dụng.
        _environment = environment;
    }

    // Kiểm tra và làm sạch trường bắt buộc; ném lỗi nếu để trống
    private static string RequiredText(string? value, string fieldName)
    {
        // 1. Tính giá trị và lưu vào `trimmed` để dùng ở bước tiếp theo.
        string trimmed = value?.Trim() ?? string.Empty;

        // 2. Kiểm tra `string.IsNullOrWhiteSpace(trimmed)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            // 3. Dừng xử lý và phát sinh lỗi `new ArgumentException($"{fieldName} không được để trống.")`.
            throw new ArgumentException($"{fieldName} không được để trống.");
        }

        // 4. Trả `trimmed` cho nơi gọi.
        return trimmed;
    }

    // Kiểm tra trường bắt buộc có giới hạn độ dài
    private static string RequiredText(string? value, string fieldName, int maxLength)
    {
        // 1. Gọi `RequiredText` và lưu kết quả vào `trimmed`.
        string trimmed = RequiredText(value, fieldName);

        // 2. Kiểm tra `trimmed.Length > maxLength` để chọn nhánh xử lý phù hợp.
        if (trimmed.Length > maxLength)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new ArgumentException($"{fieldName} tối đa {maxLength} ký tự.")`.
            throw new ArgumentException($"{fieldName} tối đa {maxLength} ký tự.");
        }

        // 4. Trả `trimmed` cho nơi gọi.
        return trimmed;
    }

    // Làm sạch trường tùy chọn; trả về null nếu chỉ có khoảng trắng
    private static string? OptionalText(string? value)
    {
        // 1. Tính giá trị và lưu vào `trimmed` để dùng ở bước tiếp theo.
        string? trimmed = value?.Trim();

        // 2. Kiểm tra `string.IsNullOrWhiteSpace(trimmed)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Trả `trimmed` cho nơi gọi.
        return trimmed;
    }

    // Extension file upload cho phép
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    // Content-Type HTTP cho phép (đối chiếu thêm ngoài extension)
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    // Lưu ảnh tải lên với tên ngẫu nhiên; kiểm tra định dạng và kích thước trước khi ghi đĩa
    private async Task<string?> SaveImageAsync(IFormFile? imageFile)
    {
        // 1. Kiểm tra `imageFile == null || imageFile.Length == 0` để chọn nhánh xử lý phù hợp.
        if (imageFile == null || imageFile.Length == 0)
        {
            // 2. Trả `null` cho nơi gọi.
            return null;
        }

        // 3. Tính giá trị và lưu vào `maxBytes` để dùng ở bước tiếp theo.
        const long maxBytes = 2 * 1024 * 1024;
        // 4. Kiểm tra `imageFile.Length > maxBytes` để chọn nhánh xử lý phù hợp.
        if (imageFile.Length > maxBytes)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new ArgumentException("Ảnh tối đa 2 MB.")`.
            throw new ArgumentException("Ảnh tối đa 2 MB.");
        }

        // 6. Gọi `GetExtension` và lưu kết quả vào `extension`.
        string extension = Path.GetExtension(imageFile.FileName);
        // 7. Kiểm tra `!AllowedImageExtensions.Contains(extension)` để chọn nhánh xử lý phù hợp.
        if (!AllowedImageExtensions.Contains(extension))
        {
            // 8. Dừng xử lý và phát sinh lỗi `new ArgumentException("Ảnh chỉ hỗ trợ JPG, PNG hoặc WebP.")`.
            throw new ArgumentException("Ảnh chỉ hỗ trợ JPG, PNG hoặc WebP.");
        }

        // 9. Kiểm tra `!AllowedImageContentTypes.Contains(imageFile.ContentType)` để chọn nhánh xử lý phù hợp.
        if (!AllowedImageContentTypes.Contains(imageFile.ContentType))
        {
            // 10. Dừng xử lý và phát sinh lỗi `new ArgumentException("Ảnh chỉ hỗ trợ JPG, PNG hoặc WebP.")`.
            throw new ArgumentException("Ảnh chỉ hỗ trợ JPG, PNG hoặc WebP.");
        }

        // 11. Gọi `OpenReadStream` và lưu kết quả vào `input`.
        await using Stream input = imageFile.OpenReadStream();
        // 12. Khai báo `image` để lưu dữ liệu dùng ở các bước sau.
        Image image;
        // 13. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 14. Gọi `DetectFormatAsync` và lưu kết quả vào `format`.
            IImageFormat? format = await Image.DetectFormatAsync(input);
            // 15. Kiểm tra `format == null || !IsAllowedImageFormat(format)` để chọn nhánh xử lý phù hợp.
            if (format == null || !IsAllowedImageFormat(format))
            {
                // 16. Dừng xử lý và phát sinh lỗi `new ArgumentException("File ảnh không đúng định dạng JPG, PNG hoặc ...`.
                throw new ArgumentException("File ảnh không đúng định dạng JPG, PNG hoặc WebP.");
            }

            // 17. Cập nhật `input.Position` bằng giá trị mới.
            input.Position = 0;
            // 18. Gọi `IdentifyAsync` và lưu kết quả vào `info`.
            ImageInfo info = await Image.IdentifyAsync(input);
            // 19. Kiểm tra `info.Width > 4096 || info.Height > 4096` để chọn nhánh xử lý phù hợp.
            if (info.Width > 4096 || info.Height > 4096)
            {
                // 20. Dừng xử lý và phát sinh lỗi `new ArgumentException("Kích thước ảnh không được vượt quá 4096 x 40...`.
                throw new ArgumentException("Kích thước ảnh không được vượt quá 4096 x 4096 pixel.");
            }

            // 21. Cập nhật `input.Position` bằng giá trị mới.
            input.Position = 0;
            // 22. Cập nhật `image` bằng giá trị mới.
            image = await Image.LoadAsync(input);
        }
        catch (UnknownImageFormatException exception)
        {
            // 23. Dừng xử lý và phát sinh lỗi `new ArgumentException("File ảnh không hợp lệ.", exception)`.
            throw new ArgumentException("File ảnh không hợp lệ.", exception);
        }
        catch (InvalidImageContentException exception)
        {
            // 24. Dừng xử lý và phát sinh lỗi `new ArgumentException("File ảnh không hợp lệ.", exception)`.
            throw new ArgumentException("File ảnh không hợp lệ.", exception);
        }

        // 25. Gọi `Combine` và lưu kết quả vào `uploadRoot`.
        string uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "flashcards");
        // 26. Gọi `CreateDirectory` để thực hiện bước nghiệp vụ này.
        Directory.CreateDirectory(uploadRoot);

        // 27. Tính giá trị và lưu vào `fileName` để dùng ở bước tiếp theo.
        string fileName = $"{Guid.NewGuid():N}.png";
        // 28. Gọi `Combine` và lưu kết quả vào `absolutePath`.
        string absolutePath = Path.Combine(uploadRoot, fileName);
        // 29. Mở tài nguyên dùng tạm và tự động giải phóng sau khi xử lý.
        using (image)
        {
            // 30. Gọi `SaveAsPngAsync` để thực hiện bước nghiệp vụ này.
            await image.SaveAsPngAsync(absolutePath);
        }

        // 31. Tính giá trị và lưu vào `maxStoredBytes` để dùng ở bước tiếp theo.
        const long maxStoredBytes = 5L * 1024 * 1024;
        // 32. Kiểm tra `new FileInfo(absolutePath).Length > maxStoredBytes` để chọn nhánh xử lý phù hợp.
        if (new FileInfo(absolutePath).Length > maxStoredBytes)
        {
            // 33. Gọi `Delete` để thực hiện bước nghiệp vụ này.
            File.Delete(absolutePath);
            // 34. Dừng xử lý và phát sinh lỗi `new ArgumentException("Ảnh sau khi xử lý vượt quá 5 MB.")`.
            throw new ArgumentException("Ảnh sau khi xử lý vượt quá 5 MB.");
        }

        // 35. Trả `$"/uploads/flashcards/{fileName}"` cho nơi gọi.
        return $"/uploads/flashcards/{fileName}";
    }

    private static bool IsAllowedImageFormat(IImageFormat format)
    {
        // 1. Trả `format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase) || f...` cho nơi gọi.
        return format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase)
        || format.Name.Equals("PNG", StringComparison.OrdinalIgnoreCase)
        || format.Name.Equals("WEBP", StringComparison.OrdinalIgnoreCase);
    }

    // Bộ chỉ thật sự công khai khi chủ sở hữu bật public và Admin chưa cách ly.
    private static bool IsPubliclyAvailable(FlashcardSet set)
    {
        // 1. Trả `set.IsPublic && set.ModerationStatus == FlashcardSetModerationStatu...` cho nơi gọi.
        return set.IsPublic
            && set.ModerationStatus == FlashcardSetModerationStatus.Active;
    }

    private void DeleteUploadedImage(string? uploadedImagePath)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(uploadedImagePath) || !uploadedImagePath....` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(uploadedImagePath)
            || !uploadedImagePath.StartsWith(UploadedImageUrlPrefix, StringComparison.Ordinal))
        {
            // 2. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 3. Tính giá trị và lưu vào `relativeName` để dùng ở bước tiếp theo.
        string relativeName = uploadedImagePath[UploadedImageUrlPrefix.Length..];
        // 4. Gọi `GetFileName` và lưu kết quả vào `fileName`.
        string fileName = Path.GetFileName(relativeName);
        // 5. Kiểm tra `!string.Equals(relativeName, fileName, StringComparison.Ordinal)` để chọn nhánh xử lý phù hợp.
        if (!string.Equals(relativeName, fileName, StringComparison.Ordinal))
        {
            // 6. Kết thúc phương thức sau khi hoàn tất xử lý.
            return;
        }

        // 7. Gọi `GetFullPath` và lưu kết quả vào `uploadRoot`.
        string uploadRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "flashcards"));
        // 8. Gọi `GetFullPath` và lưu kết quả vào `physicalPath`.
        string physicalPath = Path.GetFullPath(Path.Combine(uploadRoot, fileName));
        // 9. Kiểm tra `physicalPath.StartsWith(uploadRoot + Path.DirectorySeparatorChar, S...` để chọn nhánh xử lý phù hợp.
        if (physicalPath.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            // 10. Gọi `Delete` để thực hiện bước nghiệp vụ này.
            File.Delete(physicalPath);
        }
    }

    // Lấy tất cả bộ thẻ thuộc về một người dùng
    public async Task<List<FlashcardSet>> GetMySetsAsync(string userId)
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `sets`.
        List<FlashcardSet> sets = await _context.FlashcardSets
            // Load cards for the existing view model; project counts if set sizes grow large.
            .Include(set => set.Flashcards)
            .Where(set => set.UserId == userId)
            .OrderByDescending(set => set.UpdatedAt)
            .ToListAsync();

        // 2. Trả `sets` cho nơi gọi.
        return sets;
    }

    // Lấy tất cả bộ thẻ thuộc về một người dùng kèm tiến trình học
    public async Task<List<FlashcardSetListItemViewModel>> GetMySetsWithProgressAsync(string userId)
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `sets`.
        List<FlashcardSet> sets = await _context.FlashcardSets
            .Include(set => set.Flashcards)
            .Where(set => set.UserId == userId)
            .OrderByDescending(set => set.UpdatedAt)
            .ToListAsync();

        // 2. Khởi tạo `flashcardIds` với dữ liệu ban đầu cần thiết.
        List<int> flashcardIds = new List<int>();
        // 3. Duyệt từng `set` trong `sets` để xử lý lần lượt.
        foreach (FlashcardSet set in sets)
        {
            // 4. Duyệt từng `flashcard` trong `set.Flashcards` để xử lý lần lượt.
            foreach (Flashcard flashcard in set.Flashcards)
            {
                // 5. Gọi `Add` để thực hiện bước nghiệp vụ này.
                flashcardIds.Add(flashcard.Id);
            }
        }

        // 6. Gọi `ToListAsync` và lưu kết quả vào `learnedCardIds`.
        List<int> learnedCardIds = await _context.UserProgresses
            .Where(progress =>
                progress.UserId == userId
                && flashcardIds.Contains(progress.FlashcardId)
                && progress.IsLearned)
            .Select(progress => progress.FlashcardId)
            .ToListAsync();

        // 7. Khởi tạo `learnedCardIdSet` với dữ liệu ban đầu cần thiết.
        HashSet<int> learnedCardIdSet = new HashSet<int>(learnedCardIds);

        // 8. Khởi tạo `items` với dữ liệu ban đầu cần thiết.
        List<FlashcardSetListItemViewModel> items = new List<FlashcardSetListItemViewModel>();

        // 9. Duyệt từng `set` trong `sets` để xử lý lần lượt.
        foreach (FlashcardSet set in sets)
        {
            // 10. Tính giá trị và lưu vào `totalCards` để dùng ở bước tiếp theo.
            int totalCards = set.Flashcards.Count;
            // 11. Tính giá trị và lưu vào `learnedCount` để dùng ở bước tiếp theo.
            int learnedCount = 0;

            // 12. Duyệt từng `flashcard` trong `set.Flashcards` để xử lý lần lượt.
            foreach (Flashcard flashcard in set.Flashcards)
            {
                // 13. Kiểm tra `learnedCardIdSet.Contains(flashcard.Id)` để chọn nhánh xử lý phù hợp.
                if (learnedCardIdSet.Contains(flashcard.Id))
                {
                    // 14. Cập nhật bộ đếm hoặc trạng thái `learnedCount`.
                    learnedCount++;
                }
            }

            // 15. Tính giá trị và lưu vào `masteryPercent` để dùng ở bước tiếp theo.
            int masteryPercent = 0;
            // 16. Kiểm tra `totalCards > 0` để chọn nhánh xử lý phù hợp.
            if (totalCards > 0)
            {
                // 17. Cập nhật `masteryPercent` bằng giá trị mới.
                masteryPercent = learnedCount * 100 / totalCards;
            }

            // 18. Gọi `Add` để thực hiện bước nghiệp vụ này.
            items.Add(new FlashcardSetListItemViewModel
            {
                Id = set.Id,
                Title = set.Title,
                Description = set.Description,
                IsPublic = set.IsPublic,
                TotalCards = totalCards,
                LearnedCount = learnedCount,
                MasteryPercent = masteryPercent
            });
        }

        // 19. Trả `items` cho nơi gọi.
        return items;
    }

    // Lấy danh sách bộ thẻ public (mới nhất)
    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `sets`.
        List<FlashcardSet> sets = await _context.FlashcardSets
            .Where(set =>
                set.IsPublic
                && set.ModerationStatus == FlashcardSetModerationStatus.Active)
            .OrderByDescending(set => set.UpdatedAt)
            .Take(20)
            .ToListAsync();

        // 2. Trả `sets` cho nơi gọi.
        return sets;
    }

    // Tìm kiếm bộ thẻ public theo tiêu đề
    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `sets`.
        List<FlashcardSet> sets = await _context.FlashcardSets
            .Where(set =>
                set.IsPublic
                && set.ModerationStatus == FlashcardSetModerationStatus.Active
                && set.Title.Contains(query))
            .OrderByDescending(set => set.UpdatedAt)
            .Take(20)
            .ToListAsync();

        // 2. Trả `sets` cho nơi gọi.
        return sets;
    }

    // Lấy bộ thẻ theo id (không kèm thẻ)
    public async Task<FlashcardSet?> GetSetByIdAsync(int id)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);
        // 2. Trả `set` cho nơi gọi.
        return set;
    }

    // Lấy bộ thẻ nếu user có quyền truy cập (public hoặc chính chủ)
    public async Task<FlashcardSet?> GetAccessibleSetAsync(int id, string? userId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);

        // 2. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Tính giá trị và lưu vào `canAccess` để dùng ở bước tiếp theo.
        bool canAccess = IsPubliclyAvailable(set) || set.UserId == userId;
        // 5. Kiểm tra `!canAccess` để chọn nhánh xử lý phù hợp.
        if (!canAccess)
        {
            // 6. Trả `null` cho nơi gọi.
            return null;
        }

        // 7. Trả `set` cho nơi gọi.
        return set;
    }

    // Lấy bộ thẻ kèm thẻ; chỉ khi requester là chủ
    public async Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .Include(row => row.Flashcards.OrderBy(flashcard => flashcard.OrderIndex))
            .FirstOrDefaultAsync(row => row.Id == id);

        // 2. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Trả `set` cho nơi gọi.
        return set;
    }

    public async Task<FlashcardSet?> GetAccessibleSetWithCardsAsync(int id, string? userId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .Include(row => row.Flashcards.OrderBy(flashcard => flashcard.OrderIndex))
            .FirstOrDefaultAsync(row => row.Id == id);

        // 2. Kiểm tra `set == null` để chọn nhánh xử lý phù hợp.
        if (set == null)
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Tính giá trị và lưu vào `canAccess` để dùng ở bước tiếp theo.
        bool canAccess = IsPubliclyAvailable(set) || set.UserId == userId;
        // 5. Kiểm tra `!canAccess` để chọn nhánh xử lý phù hợp.
        if (!canAccess)
        {
            // 6. Trả `null` cho nơi gọi.
            return null;
        }

        // 7. Trả `set` cho nơi gọi.
        return set;
    }

    // Lấy bộ thẻ chỉ khi user là chủ sở hữu
    public async Task<FlashcardSet?> GetOwnedSetAsync(int id, string userId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);

        // 2. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Trả `set` cho nơi gọi.
        return set;
    }

    // Lấy thẻ kèm bộ thẻ chỉ khi user là chủ sở hữu; trả null nếu không tìm thấy, ném UnauthorizedAccessException nếu không phải chủ.
    public async Task<Flashcard?> GetCardAsync(int cardId, string userId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `card`.
        Flashcard? card = await _context.Flashcards
            .AsNoTracking()
            .Include(c => c.FlashcardSet)
            .FirstOrDefaultAsync(c => c.Id == cardId);

        // 2. Kiểm tra `card == null` để chọn nhánh xử lý phù hợp.
        if (card == null)
        {
            // 3. Trả `null` cho nơi gọi.
            return null;
        }

        // 4. Kiểm tra `card.FlashcardSet == null || card.FlashcardSet.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (card.FlashcardSet == null || card.FlashcardSet.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền xem thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền xem thẻ này.");
        }

        // 6. Trả `card` cho nơi gọi.
        return card;
    }

    // Kiểm tra user đã sao chép bộ thẻ nguồn này trước đó chưa
    public async Task<FlashcardSet?> GetExistingCopyAsync(int sourceSetId, string learnerId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `existingCopy`.
        FlashcardSet? existingCopy = await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(set =>
                set.UserId == learnerId
                && set.SourceSetId == sourceSetId);

        // 2. Trả `existingCopy` cho nơi gọi.
        return existingCopy;
    }

    // Sao chép một bộ thẻ công khai vào thư viện riêng của người dùng
    public async Task<FlashcardSet> CopyPublicSetAsync(int sourceSetId, string learnerId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `source`.
        FlashcardSet? source = await _context.FlashcardSets
            .AsNoTracking()
            .Include(set => set.Flashcards.OrderBy(flashcard => flashcard.OrderIndex))
            .FirstOrDefaultAsync(set => set.Id == sourceSetId);

        // 2. Kiểm tra `source == null || !IsPubliclyAvailable(source)` để chọn nhánh xử lý phù hợp.
        if (source == null || !IsPubliclyAvailable(source))
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Bộ thẻ nguồn không tồn tại.")`.
            throw new KeyNotFoundException("Bộ thẻ nguồn không tồn tại.");
        }

        // 4. Kiểm tra `source.UserId == learnerId` để chọn nhánh xử lý phù hợp.
        if (source.UserId == learnerId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không thể sao chép bộ thẻ của chín...`.
            throw new UnauthorizedAccessException("Không thể sao chép bộ thẻ của chính mình.");
        }

        // 6. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `existingCopy`.
        FlashcardSet? existingCopy = await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(set =>
                set.UserId == learnerId
                && set.SourceSetId == sourceSetId);

        // 7. Kiểm tra `existingCopy != null` để chọn nhánh xử lý phù hợp.
        if (existingCopy != null)
        {
            // 8. Trả `existingCopy` cho nơi gọi.
            return existingCopy;
        }

        // Guard: Clone() chỉ nhân bản thẻ đang có trên object.
        // So khớp với số thẻ trong database để tránh bản sao rỗng im lặng khi quên Include.
        // 9. Gọi `CountAsync` và lưu kết quả vào `cardCountInDatabase`.
        int cardCountInDatabase = await _context.Flashcards
            .CountAsync(flashcard => flashcard.FlashcardSetId == source.Id);

        // 10. Kiểm tra `cardCountInDatabase != source.Flashcards.Count` để chọn nhánh xử lý phù hợp.
        if (cardCountInDatabase != source.Flashcards.Count)
        {
            // 11. Dừng xử lý và phát sinh lỗi `new InvalidOperationException( "Không thể sao chép bộ thẻ: danh sác...`.
            throw new InvalidOperationException(
                "Không thể sao chép bộ thẻ: danh sách thẻ trên object không khớp số thẻ trong database. " +
                "Navigation Flashcards có thể chưa được load đủ trước khi Clone.");
        }

        // Prototype: nhân bản nội dung học; ownership và lineage gán ngay bên dưới.
        // 12. Gọi `Clone` và lưu kết quả vào `copy`.
        FlashcardSet copy = source.Clone();
        // 13. Cập nhật `copy.UserId` bằng giá trị mới.
        copy.UserId = learnerId;
        // 14. Cập nhật `copy.SourceSetId` bằng giá trị mới.
        copy.SourceSetId = source.Id;
        // Chốt nghiệp vụ: bản sao vào thư viện riêng luôn private (defense in depth).
        // 15. Cập nhật `copy.IsPublic` bằng giá trị mới.
        copy.IsPublic = false;

        // 16. Gọi `BeginTransactionAsync` và lưu kết quả vào `transaction`.
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction =
            _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

        // 17. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 18. Gọi `Add` để thực hiện bước nghiệp vụ này.
            _context.FlashcardSets.Add(copy);
            // 19. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();
            // 20. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
            // 21. Trả `copy` cho nơi gọi.
            return copy;
        }
        catch (DbUpdateException)
        {
            // 22. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            // 23. Duyệt từng `entry` trong `_context.ChangeTracker.Entries().ToList()` để xử lý lần lượt.
            _context.ChangeTracker.Clear();

            // 25. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `recovered`.
            FlashcardSet? recovered = await _context.FlashcardSets
                .FirstOrDefaultAsync(set =>
                    set.UserId == learnerId
                    && set.SourceSetId == sourceSetId);

            // 26. Kiểm tra `recovered != null` để chọn nhánh xử lý phù hợp.
            if (recovered != null)
            {
                // 27. Trả `recovered` cho nơi gọi.
                return recovered;
            }

            // 28. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
    }

    // Tạo bộ thẻ mới
    public async Task<FlashcardSet> CreateSetAsync(
        string title,
        string? description,
        bool isPublic,
        string userId,
        int? newCardQuota = null,
        bool? reviewPaused = null)
    {
        // 1. Khởi tạo `set` với dữ liệu ban đầu cần thiết.
        FlashcardSet set = new FlashcardSet
        {
            Title = RequiredText(title, "Tên bộ từ", 200),
            Description = description,
            IsPublic = isPublic,
            UserId = userId,
            NewCardQuota = ReviewSettingsPolicy.ValidateNewCardQuota(
                newCardQuota ?? ReviewSettingsPolicy.DefaultNewCardQuota),
            ReviewPaused = reviewPaused ?? false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 2. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
        await _context.FlashcardSets.AddAsync(set);
        // 3. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
        // 4. Trả `set` cho nơi gọi.
        return set;
    }

    // Cập nhật thông tin bộ thẻ
    public async Task UpdateSetAsync(
        int id,
        string title,
        string? description,
        bool isPublic,
        string userId,
        int? newCardQuota = null,
        bool? reviewPaused = null)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);

        // 2. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.");
        }

        // 4. Cập nhật `set.Title` bằng giá trị mới.
        set.Title = RequiredText(title, "Tên bộ từ", 200);
        // 5. Cập nhật `set.Description` bằng giá trị mới.
        set.Description = description;
        // 6. Cập nhật `set.IsPublic` bằng giá trị mới.
        set.IsPublic = isPublic;
        if (newCardQuota.HasValue)
        {
            set.NewCardQuota = ReviewSettingsPolicy.ValidateNewCardQuota(newCardQuota.Value);
        }

        if (reviewPaused.HasValue)
        {
            set.ReviewPaused = reviewPaused.Value;
        }
        // 7. Cập nhật `set.UpdatedAt` bằng giá trị mới.
        set.UpdatedAt = DateTime.UtcNow;

        // 8. Gọi `Update` để thực hiện bước nghiệp vụ này.
        _context.FlashcardSets.Update(set);
        // 9. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
    }

    // Xóa bộ thẻ
    public async Task DeleteSetAsync(int id, string userId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);

        // 2. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền xóa bộ thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền xóa bộ thẻ này.");
        }

        // 4. Gọi `ToListAsync` và lưu kết quả vào `uploadedImagePaths`.
        List<Flashcard> cardsToDelete = await _context.Flashcards
            .Where(card => card.FlashcardSetId == id)
            .ToListAsync();
        List<string> uploadedImagePaths = cardsToDelete
            .Where(card => card.UploadedImagePath != null)
            .Select(card => card.UploadedImagePath!)
            .ToList();
        List<int> cardIds = cardsToDelete.Select(card => card.Id).ToList();

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction =
            _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

        try
        {
            await RemoveReviewDataForCardsAsync(cardIds);

            List<UserProgress> progresses = await _context.UserProgresses
                .Where(progress => progress.Flashcard!.FlashcardSetId == id)
                .ToListAsync();
            _context.UserProgresses.RemoveRange(progresses);

            List<StudySession> studySessions = await _context.StudySessions
                .Where(session => session.FlashcardSetId == id)
                .ToListAsync();
            _context.StudySessions.RemoveRange(studySessions);

            _context.Flashcards.RemoveRange(cardsToDelete);
            _context.FlashcardSets.Remove(set);
            await _context.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            foreach (string path in uploadedImagePaths)
            {
                DeleteUploadedImage(path);
            }
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }
    }

    // Thêm thẻ mới vào bộ
    public async Task<Flashcard> AddCardAsync(
        int setId,
        string frontText,
        string backText,
        string? pronunciation,
        string? partOfSpeech,
        string? exampleSentence,
        string? exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool isStarred,
        string userId)
    {
        // 1. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .Include(row => row.Flashcards)
            .FirstOrDefaultAsync(row => row.Id == setId);

        // 2. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền thêm thẻ.")`.
            throw new UnauthorizedAccessException("Không có quyền thêm thẻ.");
        }

        // 4. Cập nhật `frontText` bằng giá trị mới.
        frontText = RequiredText(frontText, "Thuật ngữ");
        // 5. Cập nhật `backText` bằng giá trị mới.
        backText = RequiredText(backText, "Định nghĩa");
        // 6. Cập nhật `pronunciation` bằng giá trị mới.
        pronunciation = (pronunciation ?? string.Empty).Trim();
        // 7. Cập nhật `partOfSpeech` bằng giá trị mới.
        partOfSpeech = (partOfSpeech ?? string.Empty).Trim();
        // 8. Cập nhật `exampleSentence` bằng giá trị mới.
        exampleSentence = (exampleSentence ?? string.Empty).Trim();
        // 9. Cập nhật `exampleMeaning` bằng giá trị mới.
        exampleMeaning = (exampleMeaning ?? string.Empty).Trim();
        // 10. Cập nhật `synonyms` bằng giá trị mới.
        synonyms = OptionalText(synonyms);
        // 11. Cập nhật `imageUrl` bằng giá trị mới.
        imageUrl = OptionalText(imageUrl);
        // 12. Gọi `SaveImageAsync` và lưu kết quả vào `uploadedImagePath`.
        string? uploadedImagePath = await SaveImageAsync(imageFile);

        // 13. Tính giá trị và lưu vào `nextOrder` để dùng ở bước tiếp theo.
        int nextOrder = 0;
        // 14. Kiểm tra `set.Flashcards.Any()` để chọn nhánh xử lý phù hợp.
        if (set.Flashcards.Any())
        {
            // 15. Cập nhật `nextOrder` bằng giá trị mới.
            nextOrder = set.Flashcards.Max(card => card.OrderIndex) + 1;
        }

        // 16. Khởi tạo `card` với dữ liệu ban đầu cần thiết.
        Flashcard card = new Flashcard
        {
            FlashcardSetId = setId,
            FrontText = frontText,
            BackText = backText,
            Pronunciation = pronunciation,
            PartOfSpeech = partOfSpeech,
            ExampleSentence = exampleSentence,
            ExampleMeaning = exampleMeaning,
            Synonyms = synonyms,
            ImageUrl = imageUrl,
            UploadedImagePath = uploadedImagePath,
            IsStarred = isStarred,
            OrderIndex = nextOrder
        };

        // 17. Gọi `AddAsync` để thực hiện bước nghiệp vụ này.
        await _context.Flashcards.AddAsync(card);
        // 18. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 19. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();
        }
        catch
        {
            // 20. Gọi `DeleteUploadedImage` để thực hiện bước nghiệp vụ này.
            DeleteUploadedImage(uploadedImagePath);
            // 21. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
        // 22. Trả `card` cho nơi gọi.
        return card;
    }

    // Cập nhật nội dung thẻ (mặt trước + mặt sau)
    public async Task<int> UpdateCardAsync(
        int cardId,
        string frontText,
        string backText,
        string? pronunciation,
        string? partOfSpeech,
        string? exampleSentence,
        string? exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool removeUploadedImage,
        bool isStarred,
        string userId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `card`.
        Flashcard? card = await _context.Flashcards.FindAsync(cardId);
        // 2. Kiểm tra `card == null` để chọn nhánh xử lý phù hợp.
        if (card == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Thẻ không tồn tại.")`.
            throw new KeyNotFoundException("Thẻ không tồn tại.");
        }

        // 4. Tính giá trị và lưu vào `setId` để dùng ở bước tiếp theo.
        int setId = card.FlashcardSetId;
        // 5. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);

        // 6. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền sửa thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền sửa thẻ này.");
        }

        // 8. Cập nhật `card.FrontText` bằng giá trị mới.
        card.FrontText = RequiredText(frontText, "Thuật ngữ");
        // 9. Cập nhật `card.BackText` bằng giá trị mới.
        card.BackText = RequiredText(backText, "Định nghĩa");
        // 10. Cập nhật `card.Pronunciation` bằng giá trị mới.
        card.Pronunciation = (pronunciation ?? string.Empty).Trim();
        // 11. Cập nhật `card.PartOfSpeech` bằng giá trị mới.
        card.PartOfSpeech = (partOfSpeech ?? string.Empty).Trim();
        // 12. Cập nhật `card.ExampleSentence` bằng giá trị mới.
        card.ExampleSentence = (exampleSentence ?? string.Empty).Trim();
        // 13. Cập nhật `card.ExampleMeaning` bằng giá trị mới.
        card.ExampleMeaning = (exampleMeaning ?? string.Empty).Trim();
        // 14. Cập nhật `card.Synonyms` bằng giá trị mới.
        card.Synonyms = OptionalText(synonyms);
        // 15. Cập nhật `card.ImageUrl` bằng giá trị mới.
        card.ImageUrl = OptionalText(imageUrl);

        // 16. Tính giá trị và lưu vào `oldUploadedImagePath` để dùng ở bước tiếp theo.
        string? oldUploadedImagePath = card.UploadedImagePath;
        // 17. Kiểm tra `removeUploadedImage` để chọn nhánh xử lý phù hợp.
        if (removeUploadedImage)
        {
            // 18. Cập nhật `card.UploadedImagePath` bằng giá trị mới.
            card.UploadedImagePath = null;
        }

        // 19. Gọi `SaveImageAsync` và lưu kết quả vào `newUpload`.
        string? newUpload = await SaveImageAsync(imageFile);
        // 20. Kiểm tra `newUpload != null` để chọn nhánh xử lý phù hợp.
        if (newUpload != null)
        {
            // 21. Cập nhật `card.UploadedImagePath` bằng giá trị mới.
            card.UploadedImagePath = newUpload;
        }

        // 22. Cập nhật `card.IsStarred` bằng giá trị mới.
        card.IsStarred = isStarred;

        // 23. Gọi `Update` để thực hiện bước nghiệp vụ này.
        _context.Flashcards.Update(card);
        // 24. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            // 25. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();
        }
        catch
        {
            // 26. Kiểm tra `!string.Equals(newUpload, oldUploadedImagePath, StringComparison.Or...` để chọn nhánh xử lý phù hợp.
            if (!string.Equals(newUpload, oldUploadedImagePath, StringComparison.Ordinal))
            {
                // 27. Gọi `DeleteUploadedImage` để thực hiện bước nghiệp vụ này.
                DeleteUploadedImage(newUpload);
            }
            // 28. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }

        // 29. Kiểm tra `!string.Equals(oldUploadedImagePath, card.UploadedImagePath, String...` để chọn nhánh xử lý phù hợp.
        if (!string.Equals(oldUploadedImagePath, card.UploadedImagePath, StringComparison.Ordinal))
        {
            // 30. Gọi `DeleteUploadedImage` để thực hiện bước nghiệp vụ này.
            DeleteUploadedImage(oldUploadedImagePath);
        }
        // 31. Trả `setId` cho nơi gọi.
        return setId;
    }

    // Xóa thẻ khỏi bộ
    public async Task<int> DeleteCardAsync(int cardId, string userId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `card`.
        Flashcard? card = await _context.Flashcards.FindAsync(cardId);
        // 2. Kiểm tra `card == null` để chọn nhánh xử lý phù hợp.
        if (card == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Thẻ không tồn tại.")`.
            throw new KeyNotFoundException("Thẻ không tồn tại.");
        }

        // 4. Tính giá trị và lưu vào `setId` để dùng ở bước tiếp theo.
        int setId = card.FlashcardSetId;
        // 5. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);

        // 6. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 7. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền xóa thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền xóa thẻ này.");
        }

        // 8. Tính giá trị và lưu vào `transaction` để dùng ở bước tiếp theo.
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        // 9. Thực hiện khối nghiệp vụ và chuyển lỗi sang nhánh xử lý tương ứng.
        try
        {
            await RemoveReviewDataForCardsAsync([cardId]);

            // 10. Gọi `ExecuteDeleteAsync` để thực hiện bước nghiệp vụ này.
            List<UserProgress> progresses = await _context.UserProgresses
                .Where(progress => progress.FlashcardId == cardId)
                .ToListAsync();
            _context.UserProgresses.RemoveRange(progresses);

            // 11. Gọi `ToListAsync` và lưu kết quả vào `details`.
            List<DictationSessionDetail> details = await _context.DictationSessionDetails
                .Where(detail => detail.FlashcardId == cardId)
                .ToListAsync();
            _context.DictationSessionDetails.RemoveRange(details);

            // 12. Gọi `ToListAsync` và lưu kết quả vào `missionWords`.
            List<EnglishMissionTargetWord> missionWords = await _context.EnglishMissionTargetWords
                .Where(word => word.FlashcardId == cardId)
                .ToListAsync();
            _context.EnglishMissionTargetWords.RemoveRange(missionWords);

            // 13. Tính giá trị và lưu vào `uploadedImagePath` để dùng ở bước tiếp theo.
            string? uploadedImagePath = card.UploadedImagePath;
            // 14. Gọi `Remove` để thực hiện bước nghiệp vụ này.
            _context.Flashcards.Remove(card);
            // 15. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
            await _context.SaveChangesAsync();
            // 16. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 17. Gọi `CommitAsync` để thực hiện bước nghiệp vụ này.
                await transaction.CommitAsync();
            }

            // 18. Gọi `DeleteUploadedImage` để thực hiện bước nghiệp vụ này.
            DeleteUploadedImage(uploadedImagePath);
            // 19. Trả `setId` cho nơi gọi.
            return setId;
        }
        catch
        {
            // 20. Kiểm tra `transaction != null` để chọn nhánh xử lý phù hợp.
            if (transaction != null)
            {
                // 21. Gọi `RollbackAsync` để thực hiện bước nghiệp vụ này.
                await transaction.RollbackAsync();
            }

            // 22. Phát sinh lại lỗi hiện tại để tầng gọi xử lý.
            throw;
        }
    }

    // Xóa toàn bộ thẻ trong một bộ thẻ (chủ sở hữu).
    // Load một lần rồi xóa tập trung, tránh vòng lặp xóa từng thẻ (N+1).
    public async Task DeleteAllCardsAsync(int setId, string userId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);

        // 2. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền xóa thẻ trong bộ th...`.
            throw new UnauthorizedAccessException("Không có quyền xóa thẻ trong bộ thẻ này.");
        }

        // 4. Gọi `RemoveAllCardsInternalAsync` và lưu kết quả vào `uploadedImagePaths`.
        List<string> uploadedImagePaths = await RemoveAllCardsInternalAsync(setId);
        // 5. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
        // 6. Duyệt từng `path` trong `uploadedImagePaths` để xử lý lần lượt.
        foreach (string path in uploadedImagePaths)
        {
            // 7. Gọi `DeleteUploadedImage` để thực hiện bước nghiệp vụ này.
            DeleteUploadedImage(path);
        }
    }

    // Xóa progress + thẻ của một bộ nhưng KHÔNG SaveChanges — dùng chung cho batch import trong transaction.
    private async Task<List<string>> RemoveAllCardsInternalAsync(int setId)
    {
        // 1. Gọi `ToListAsync` và lưu kết quả vào `progresses`.
        List<UserProgress> progresses = await _context.UserProgresses
            .Where(progress => progress.Flashcard!.FlashcardSetId == setId)
            .ToListAsync();
        // 2. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
        _context.UserProgresses.RemoveRange(progresses);

        // 3. Gọi `ToListAsync` và lưu kết quả vào `details`.
        List<DictationSessionDetail> details = await _context.DictationSessionDetails
            .Where(detail => detail.Flashcard!.FlashcardSetId == setId)
            .ToListAsync();
        // 4. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
        _context.DictationSessionDetails.RemoveRange(details);

        // 5. Gọi `ToListAsync` và lưu kết quả vào `missionWords`.
        List<EnglishMissionTargetWord> missionWords = await _context.EnglishMissionTargetWords
            .Where(word => word.Flashcard!.FlashcardSetId == setId)
            .ToListAsync();
        // 6. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
        _context.EnglishMissionTargetWords.RemoveRange(missionWords);

        // 7. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await _context.Flashcards
            .Where(card => card.FlashcardSetId == setId)
            .ToListAsync();
        await RemoveReviewDataForCardsAsync(cards.Select(card => card.Id).ToArray());
        // 8. Gọi `RemoveRange` để thực hiện bước nghiệp vụ này.
        _context.Flashcards.RemoveRange(cards);
        // 9. Trả kết quả từ `ToList` cho nơi gọi.
        return cards
            .Select(card => card.UploadedImagePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
    }

    private async Task RemoveReviewDataForCardsAsync(IReadOnlyCollection<int> cardIds)
    {
        if (cardIds.Count == 0)
        {
            return;
        }

        List<ReviewProgress> progresses = await _context.ReviewProgresses
            .Where(progress => cardIds.Contains(progress.FlashcardId))
            .ToListAsync();
        _context.ReviewProgresses.RemoveRange(progresses);

        List<ReviewSessionItem> items = await _context.ReviewSessionItems
            .Where(item => cardIds.Contains(item.FlashcardId))
            .ToListAsync();
        _context.ReviewSessionItems.RemoveRange(items);

        int[] affectedSessionIds = items
            .Select(item => item.ReviewSessionId)
            .Distinct()
            .ToArray();
        if (affectedSessionIds.Length == 0)
        {
            return;
        }

        List<ReviewSession> sessions = await _context.ReviewSessions
            .Include(session => session.Items)
            .Where(session => affectedSessionIds.Contains(session.Id))
            .ToListAsync();
        foreach (ReviewSession session in sessions)
        {
            bool hasRemainingItem = session.Items.Any(item =>
                !cardIds.Contains(item.FlashcardId)
                && _context.Entry(item).State != EntityState.Deleted);
            if (!hasRemainingItem
                && session.CompletedAtUtc == null
                && session.EndedAtUtc == null)
            {
                _context.ReviewSessions.Remove(session);
            }
        }
    }

    // Import hàng loạt thẻ một cách nguyên tử:
    // xóa (nếu replaceAll) + thêm mới được gộp trong MỘT lần SaveChangesAsync,
    // EF Core tự bọc trong một transaction — lỗi giữa chừng thì rollback, không mất dữ liệu.
    // (Không dùng BeginTransactionAsync tường minh để tương thích InMemory provider trong test.)
    public async Task<List<Flashcard>> BatchImportCardsAsync(
        int setId,
        IReadOnlyList<BatchImportCardItem> cards,
        bool replaceAll,
        string userId)
    {
        // 1. Kiểm tra `cards.Count > FlashcardImportValidation.MaxRows` để chọn nhánh xử lý phù hợp.
        if (cards.Count > FlashcardImportValidation.MaxRows)
        {
            // 2. Dừng xử lý và phát sinh lỗi `new ArgumentException($"Mỗi lần chỉ được nhập tối đa {FlashcardImpo...`.
            throw new ArgumentException($"Mỗi lần chỉ được nhập tối đa {FlashcardImportValidation.MaxRows} thẻ.");
        }

        // 3. Gọi `FirstOrDefaultAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets
            .Include(row => row.Flashcards)
            .FirstOrDefaultAsync(row => row.Id == setId);

        // 4. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 5. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền thêm thẻ.")`.
            throw new UnauthorizedAccessException("Không có quyền thêm thẻ.");
        }

        // 6. Tính giá trị và lưu vào `nextOrder` để dùng ở bước tiếp theo.
        int nextOrder = 0;
        // 7. Khởi tạo `uploadedImagePaths` với dữ liệu ban đầu cần thiết.
        List<string> uploadedImagePaths = [];
        // 8. Kiểm tra `replaceAll` để chọn nhánh xử lý phù hợp.
        if (replaceAll)
        {
            // 9. Cập nhật `uploadedImagePaths` bằng giá trị mới.
            uploadedImagePaths = await RemoveAllCardsInternalAsync(setId);
        }
        else if (set.Flashcards.Any())
        {
            // 10. Cập nhật `nextOrder` bằng giá trị mới.
            nextOrder = set.Flashcards.Max(card => card.OrderIndex) + 1;
        }

        // 11. Khởi tạo `created` với dữ liệu ban đầu cần thiết.
        List<Flashcard> created = new List<Flashcard>();
        // 12. Duyệt từng `item` trong `cards` để xử lý lần lượt.
        foreach (BatchImportCardItem item in cards)
        {
            // 13. Khởi tạo `card` với dữ liệu ban đầu cần thiết.
            Flashcard card = new Flashcard
            {
                FlashcardSetId = setId,
                FrontText = RequiredText(item.FrontText, "Thuật ngữ"),
                BackText = RequiredText(item.BackText, "Định nghĩa"),
                Pronunciation = (item.Pronunciation ?? string.Empty).Trim(),
                PartOfSpeech = (item.PartOfSpeech ?? string.Empty).Trim(),
                ExampleSentence = (item.ExampleSentence ?? string.Empty).Trim(),
                ExampleMeaning = (item.ExampleMeaning ?? string.Empty).Trim(),
                Synonyms = OptionalText(item.Synonyms),
                ImageUrl = OptionalText(item.ImageUrl),
                UploadedImagePath = null,
                IsStarred = item.IsStarred,
                OrderIndex = nextOrder
            };

            // 14. Cập nhật bộ đếm hoặc trạng thái `nextOrder`.
            nextOrder++;
            // 15. Gọi `Add` để thực hiện bước nghiệp vụ này.
            created.Add(card);
        }

        // 16. Gọi `AddRangeAsync` để thực hiện bước nghiệp vụ này.
        await _context.Flashcards.AddRangeAsync(created);
        // 17. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
        // 18. Duyệt từng `path` trong `uploadedImagePaths` để xử lý lần lượt.
        foreach (string path in uploadedImagePaths)
        {
            // 19. Gọi `DeleteUploadedImage` để thực hiện bước nghiệp vụ này.
            DeleteUploadedImage(path);
        }
        // 20. Trả `created` cho nơi gọi.
        return created;
    }

    // Đổi trạng thái đánh sao của thẻ
    public async Task<bool> ToggleStarAsync(int cardId, string userId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `card`.
        Flashcard? card = await _context.Flashcards.FindAsync(cardId);
        // 2. Kiểm tra `card == null` để chọn nhánh xử lý phù hợp.
        if (card == null)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new KeyNotFoundException("Thẻ không tồn tại.")`.
            throw new KeyNotFoundException("Thẻ không tồn tại.");
        }

        // 4. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(card.FlashcardSetId);

        // 5. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 6. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền chỉnh sửa thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền chỉnh sửa thẻ này.");
        }

        // 7. Cập nhật `card.IsStarred` bằng giá trị mới.
        card.IsStarred = !card.IsStarred;
        // 8. Gọi `Update` để thực hiện bước nghiệp vụ này.
        _context.Flashcards.Update(card);
        // 9. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
        // 10. Trả `card.IsStarred` cho nơi gọi.
        return card.IsStarred;
    }

    // Cập nhật thứ tự các thẻ theo mảng id được sắp xếp
    public async Task ReorderCardsAsync(int setId, int[] orderedCardIds, string userId)
    {
        // 1. Gọi `FindAsync` và lưu kết quả vào `set`.
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);
        // 2. Kiểm tra `set == null || set.UserId != userId` để chọn nhánh xử lý phù hợp.
        if (set == null || set.UserId != userId)
        {
            // 3. Dừng xử lý và phát sinh lỗi `new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.")`.
            throw new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.");
        }

        // 4. Gọi `ToListAsync` và lưu kết quả vào `cards`.
        List<Flashcard> cards = await _context.Flashcards
            .Where(c => c.FlashcardSetId == setId)
            .ToListAsync();

        // 5. Gọi `ToHashSet` và lưu kết quả vào `cardIds`.
        HashSet<int> cardIds = cards.Select(c => c.Id).ToHashSet();
        // 6. Gọi `ToHashSet` và lưu kết quả vào `orderedIds`.
        HashSet<int> orderedIds = orderedCardIds.ToHashSet();

        // 7. Gọi `ToList` và lưu kết quả vào `unknownIds`.
        List<int> unknownIds = orderedIds.Except(cardIds).ToList();
        // 8. Kiểm tra `unknownIds.Count > 0` để chọn nhánh xử lý phù hợp.
        if (unknownIds.Count > 0)
        {
            // 9. Dừng xử lý và phát sinh lỗi `new ArgumentException($"Các id thẻ không thuộc bộ thẻ: {string.Join...`.
            throw new ArgumentException($"Các id thẻ không thuộc bộ thẻ: {string.Join(", ", unknownIds)}.");
        }

        // 10. Gọi `ToList` và lưu kết quả vào `missingIds`.
        List<int> missingIds = cardIds.Except(orderedIds).ToList();
        // 11. Kiểm tra `missingIds.Count > 0` để chọn nhánh xử lý phù hợp.
        if (missingIds.Count > 0)
        {
            // 12. Dừng xử lý và phát sinh lỗi `new ArgumentException($"Thiếu thứ tự cho các thẻ: {string.Join(", "...`.
            throw new ArgumentException($"Thiếu thứ tự cho các thẻ: {string.Join(", ", missingIds)}.");
        }

        // 13. Kiểm tra `orderedCardIds.Length != cardIds.Count` để chọn nhánh xử lý phù hợp.
        if (orderedCardIds.Length != cardIds.Count)
        {
            // 14. Dừng xử lý và phát sinh lỗi `new ArgumentException("Danh sách thứ tự chứa id thẻ trùng lặp.")`.
            throw new ArgumentException("Danh sách thứ tự chứa id thẻ trùng lặp.");
        }

        // 15. Gọi `ToDictionary` và lưu kết quả vào `cardMap`.
        Dictionary<int, Flashcard> cardMap = cards.ToDictionary(c => c.Id);
        // 16. Lặp qua phạm vi dữ liệu cần xử lý.
        for (int i = 0; i < orderedCardIds.Length; i++)
        {
            // 17. Cập nhật `cardMap[orderedCardIds[i]].OrderIndex` bằng giá trị mới.
            cardMap[orderedCardIds[i]].OrderIndex = i;
        }

        // 18. Gọi `SaveChangesAsync` để thực hiện bước nghiệp vụ này.
        await _context.SaveChangesAsync();
    }
}
