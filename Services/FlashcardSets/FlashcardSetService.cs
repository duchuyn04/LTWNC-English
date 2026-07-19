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
        _context = context;
        _environment = environment;
    }

    // Kiểm tra và làm sạch trường bắt buộc; ném lỗi nếu để trống
    private static string RequiredText(string? value, string fieldName)
    {
        string trimmed = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"{fieldName} không được để trống.");
        }

        return trimmed;
    }

    // Kiểm tra trường bắt buộc có giới hạn độ dài
    private static string RequiredText(string? value, string fieldName, int maxLength)
    {
        string trimmed = RequiredText(value, fieldName);

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} tối đa {maxLength} ký tự.");
        }

        return trimmed;
    }

    // Làm sạch trường tùy chọn; trả về null nếu chỉ có khoảng trắng
    private static string? OptionalText(string? value)
    {
        string? trimmed = value?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

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
        if (imageFile == null || imageFile.Length == 0)
        {
            return null;
        }

        const long maxBytes = 2 * 1024 * 1024;
        if (imageFile.Length > maxBytes)
        {
            throw new ArgumentException("Ảnh tối đa 2 MB.");
        }

        string extension = Path.GetExtension(imageFile.FileName);
        if (!AllowedImageExtensions.Contains(extension))
        {
            throw new ArgumentException("Ảnh chỉ hỗ trợ JPG, PNG hoặc WebP.");
        }

        if (!AllowedImageContentTypes.Contains(imageFile.ContentType))
        {
            throw new ArgumentException("Ảnh chỉ hỗ trợ JPG, PNG hoặc WebP.");
        }

        await using Stream input = imageFile.OpenReadStream();
        Image image;
        try
        {
            IImageFormat? format = await Image.DetectFormatAsync(input);
            if (format == null || !IsAllowedImageFormat(format))
            {
                throw new ArgumentException("File ảnh không đúng định dạng JPG, PNG hoặc WebP.");
            }

            input.Position = 0;
            ImageInfo info = await Image.IdentifyAsync(input);
            if (info.Width > 4096 || info.Height > 4096)
            {
                throw new ArgumentException("Kích thước ảnh không được vượt quá 4096 x 4096 pixel.");
            }

            input.Position = 0;
            image = await Image.LoadAsync(input);
        }
        catch (UnknownImageFormatException exception)
        {
            throw new ArgumentException("File ảnh không hợp lệ.", exception);
        }
        catch (InvalidImageContentException exception)
        {
            throw new ArgumentException("File ảnh không hợp lệ.", exception);
        }

        string uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "flashcards");
        Directory.CreateDirectory(uploadRoot);

        string fileName = $"{Guid.NewGuid():N}.png";
        string absolutePath = Path.Combine(uploadRoot, fileName);
        using (image)
        {
            await image.SaveAsPngAsync(absolutePath);
        }

        const long maxStoredBytes = 5L * 1024 * 1024;
        if (new FileInfo(absolutePath).Length > maxStoredBytes)
        {
            File.Delete(absolutePath);
            throw new ArgumentException("Ảnh sau khi xử lý vượt quá 5 MB.");
        }

        return $"/uploads/flashcards/{fileName}";
    }

    private static bool IsAllowedImageFormat(IImageFormat format) =>
        format.Name.Equals("JPEG", StringComparison.OrdinalIgnoreCase)
        || format.Name.Equals("PNG", StringComparison.OrdinalIgnoreCase)
        || format.Name.Equals("WEBP", StringComparison.OrdinalIgnoreCase);

    private void DeleteUploadedImage(string? uploadedImagePath)
    {
        if (string.IsNullOrWhiteSpace(uploadedImagePath)
            || !uploadedImagePath.StartsWith(UploadedImageUrlPrefix, StringComparison.Ordinal))
        {
            return;
        }

        string relativeName = uploadedImagePath[UploadedImageUrlPrefix.Length..];
        string fileName = Path.GetFileName(relativeName);
        if (!string.Equals(relativeName, fileName, StringComparison.Ordinal))
        {
            return;
        }

        string uploadRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "flashcards"));
        string physicalPath = Path.GetFullPath(Path.Combine(uploadRoot, fileName));
        if (physicalPath.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(physicalPath);
        }
    }

    // Lấy tất cả bộ thẻ thuộc về một người dùng
    public async Task<List<FlashcardSet>> GetMySetsAsync(string userId)
    {
        List<FlashcardSet> sets = await _context.FlashcardSets
            // Load cards for the existing view model; project counts if set sizes grow large.
            .Include(set => set.Flashcards)
            .Where(set => set.UserId == userId)
            .OrderByDescending(set => set.UpdatedAt)
            .ToListAsync();

        return sets;
    }

    // Lấy tất cả bộ thẻ thuộc về một người dùng kèm tiến trình học
    public async Task<List<FlashcardSetListItemViewModel>> GetMySetsWithProgressAsync(string userId)
    {
        List<FlashcardSet> sets = await _context.FlashcardSets
            .Include(set => set.Flashcards)
            .Where(set => set.UserId == userId)
            .OrderByDescending(set => set.UpdatedAt)
            .ToListAsync();

        List<int> flashcardIds = new List<int>();
        foreach (FlashcardSet set in sets)
        {
            foreach (Flashcard flashcard in set.Flashcards)
            {
                flashcardIds.Add(flashcard.Id);
            }
        }

        List<int> learnedCardIds = await _context.UserProgresses
            .Where(progress =>
                progress.UserId == userId
                && flashcardIds.Contains(progress.FlashcardId)
                && progress.IsLearned)
            .Select(progress => progress.FlashcardId)
            .ToListAsync();

        HashSet<int> learnedCardIdSet = new HashSet<int>(learnedCardIds);

        List<FlashcardSetListItemViewModel> items = new List<FlashcardSetListItemViewModel>();

        foreach (FlashcardSet set in sets)
        {
            int totalCards = set.Flashcards.Count;
            int learnedCount = 0;

            foreach (Flashcard flashcard in set.Flashcards)
            {
                if (learnedCardIdSet.Contains(flashcard.Id))
                {
                    learnedCount++;
                }
            }

            int masteryPercent = 0;
            if (totalCards > 0)
            {
                masteryPercent = learnedCount * 100 / totalCards;
            }

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

        return items;
    }

    // Lấy danh sách bộ thẻ public (mới nhất)
    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        List<FlashcardSet> sets = await _context.FlashcardSets
            .Where(set => set.IsPublic)
            .OrderByDescending(set => set.UpdatedAt)
            .Take(20)
            .ToListAsync();

        return sets;
    }

    // Tìm kiếm bộ thẻ public theo tiêu đề
    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        List<FlashcardSet> sets = await _context.FlashcardSets
            .Where(set => set.IsPublic && set.Title.Contains(query))
            .OrderByDescending(set => set.UpdatedAt)
            .Take(20)
            .ToListAsync();

        return sets;
    }

    // Lấy bộ thẻ theo id (không kèm thẻ)
    public async Task<FlashcardSet?> GetSetByIdAsync(int id)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);
        return set;
    }

    // Lấy bộ thẻ nếu user có quyền truy cập (public hoặc chính chủ)
    public async Task<FlashcardSet?> GetAccessibleSetAsync(int id, string? userId)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);

        if (set == null)
        {
            return null;
        }

        bool canAccess = set.IsPublic || set.UserId == userId;
        if (!canAccess)
        {
            return null;
        }

        return set;
    }

    // Lấy bộ thẻ kèm thẻ; chỉ khi requester là chủ
    public async Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId)
    {
        FlashcardSet? set = await _context.FlashcardSets
            .Include(row => row.Flashcards.OrderBy(flashcard => flashcard.OrderIndex))
            .FirstOrDefaultAsync(row => row.Id == id);

        if (set == null || set.UserId != userId)
        {
            return null;
        }

        return set;
    }

    public async Task<FlashcardSet?> GetAccessibleSetWithCardsAsync(int id, string? userId)
    {
        FlashcardSet? set = await _context.FlashcardSets
            .Include(row => row.Flashcards.OrderBy(flashcard => flashcard.OrderIndex))
            .FirstOrDefaultAsync(row => row.Id == id);

        if (set == null)
        {
            return null;
        }

        bool canAccess = set.IsPublic || set.UserId == userId;
        if (!canAccess)
        {
            return null;
        }

        return set;
    }

    // Lấy bộ thẻ chỉ khi user là chủ sở hữu
    public async Task<FlashcardSet?> GetOwnedSetAsync(int id, string userId)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);

        if (set == null || set.UserId != userId)
        {
            return null;
        }

        return set;
    }

    // Lấy thẻ kèm bộ thẻ chỉ khi user là chủ sở hữu; trả null nếu không tìm thấy, ném UnauthorizedAccessException nếu không phải chủ.
    public async Task<Flashcard?> GetCardAsync(int cardId, string userId)
    {
        Flashcard? card = await _context.Flashcards
            .AsNoTracking()
            .Include(c => c.FlashcardSet)
            .FirstOrDefaultAsync(c => c.Id == cardId);

        if (card == null)
        {
            return null;
        }

        if (card.FlashcardSet == null || card.FlashcardSet.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền xem thẻ này.");
        }

        return card;
    }

    // Kiểm tra user đã sao chép bộ thẻ nguồn này trước đó chưa
    public async Task<FlashcardSet?> GetExistingCopyAsync(int sourceSetId, string learnerId)
    {
        FlashcardSet? existingCopy = await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(set =>
                set.UserId == learnerId
                && set.SourceSetId == sourceSetId);

        return existingCopy;
    }

    // Sao chép một bộ thẻ công khai vào thư viện riêng của người dùng
    public async Task<FlashcardSet> CopyPublicSetAsync(int sourceSetId, string learnerId)
    {
        FlashcardSet? source = await _context.FlashcardSets
            .AsNoTracking()
            .Include(set => set.Flashcards.OrderBy(flashcard => flashcard.OrderIndex))
            .FirstOrDefaultAsync(set => set.Id == sourceSetId);

        if (source == null || !source.IsPublic)
        {
            throw new KeyNotFoundException("Bộ thẻ nguồn không tồn tại.");
        }

        if (source.UserId == learnerId)
        {
            throw new UnauthorizedAccessException("Không thể sao chép bộ thẻ của chính mình.");
        }

        FlashcardSet? existingCopy = await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(set =>
                set.UserId == learnerId
                && set.SourceSetId == sourceSetId);

        if (existingCopy != null)
        {
            return existingCopy;
        }

        // Guard: Clone() chỉ nhân bản thẻ đang có trên object.
        // So khớp với số thẻ trong database để tránh bản sao rỗng im lặng khi quên Include.
        int cardCountInDatabase = await _context.Flashcards
            .CountAsync(flashcard => flashcard.FlashcardSetId == source.Id);

        if (cardCountInDatabase != source.Flashcards.Count)
        {
            throw new InvalidOperationException(
                "Không thể sao chép bộ thẻ: danh sách thẻ trên object không khớp số thẻ trong database. " +
                "Navigation Flashcards có thể chưa được load đủ trước khi Clone.");
        }

        // Prototype: nhân bản nội dung học; ownership và lineage gán ngay bên dưới.
        FlashcardSet copy = source.Clone();
        copy.UserId = learnerId;
        copy.SourceSetId = source.Id;
        // Chốt nghiệp vụ: bản sao vào thư viện riêng luôn private (defense in depth).
        copy.IsPublic = false;

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.FlashcardSets.Add(copy);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return copy;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry
                     in _context.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }

            FlashcardSet? recovered = await _context.FlashcardSets
                .FirstOrDefaultAsync(set =>
                    set.UserId == learnerId
                    && set.SourceSetId == sourceSetId);

            if (recovered != null)
            {
                return recovered;
            }

            throw;
        }
    }

    // Tạo bộ thẻ mới
    public async Task<FlashcardSet> CreateSetAsync(
        string title,
        string? description,
        bool isPublic,
        string userId)
    {
        FlashcardSet set = new FlashcardSet
        {
            Title = RequiredText(title, "Tên bộ từ", 200),
            Description = description,
            IsPublic = isPublic,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.FlashcardSets.AddAsync(set);
        await _context.SaveChangesAsync();
        return set;
    }

    // Cập nhật thông tin bộ thẻ
    public async Task UpdateSetAsync(
        int id,
        string title,
        string? description,
        bool isPublic,
        string userId)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);

        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.");
        }

        set.Title = RequiredText(title, "Tên bộ từ", 200);
        set.Description = description;
        set.IsPublic = isPublic;
        set.UpdatedAt = DateTime.UtcNow;

        _context.FlashcardSets.Update(set);
        await _context.SaveChangesAsync();
    }

    // Xóa bộ thẻ
    public async Task DeleteSetAsync(int id, string userId)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(id);

        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền xóa bộ thẻ này.");
        }

        List<string> uploadedImagePaths = await _context.Flashcards
            .Where(card => card.FlashcardSetId == id && card.UploadedImagePath != null)
            .Select(card => card.UploadedImagePath!)
            .ToListAsync();

        await _context.UserProgresses
            .Where(progress => progress.Flashcard!.FlashcardSetId == id)
            .ExecuteDeleteAsync();

        await _context.StudySessions
            .Where(session => session.FlashcardSetId == id)
            .ExecuteDeleteAsync();

        _context.FlashcardSets.Remove(set);
        await _context.SaveChangesAsync();
        foreach (string path in uploadedImagePaths)
        {
            DeleteUploadedImage(path);
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
        FlashcardSet? set = await _context.FlashcardSets
            .Include(row => row.Flashcards)
            .FirstOrDefaultAsync(row => row.Id == setId);

        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền thêm thẻ.");
        }

        frontText = RequiredText(frontText, "Thuật ngữ");
        backText = RequiredText(backText, "Định nghĩa");
        pronunciation = (pronunciation ?? string.Empty).Trim();
        partOfSpeech = (partOfSpeech ?? string.Empty).Trim();
        exampleSentence = (exampleSentence ?? string.Empty).Trim();
        exampleMeaning = (exampleMeaning ?? string.Empty).Trim();
        synonyms = OptionalText(synonyms);
        imageUrl = OptionalText(imageUrl);
        string? uploadedImagePath = await SaveImageAsync(imageFile);

        int nextOrder = 0;
        if (set.Flashcards.Any())
        {
            nextOrder = set.Flashcards.Max(card => card.OrderIndex) + 1;
        }

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

        await _context.Flashcards.AddAsync(card);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            DeleteUploadedImage(uploadedImagePath);
            throw;
        }
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
        Flashcard? card = await _context.Flashcards.FindAsync(cardId);
        if (card == null)
        {
            throw new KeyNotFoundException("Thẻ không tồn tại.");
        }

        int setId = card.FlashcardSetId;
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);

        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền sửa thẻ này.");
        }

        card.FrontText = RequiredText(frontText, "Thuật ngữ");
        card.BackText = RequiredText(backText, "Định nghĩa");
        card.Pronunciation = (pronunciation ?? string.Empty).Trim();
        card.PartOfSpeech = (partOfSpeech ?? string.Empty).Trim();
        card.ExampleSentence = (exampleSentence ?? string.Empty).Trim();
        card.ExampleMeaning = (exampleMeaning ?? string.Empty).Trim();
        card.Synonyms = OptionalText(synonyms);
        card.ImageUrl = OptionalText(imageUrl);

        string? oldUploadedImagePath = card.UploadedImagePath;
        if (removeUploadedImage)
        {
            card.UploadedImagePath = null;
        }

        string? newUpload = await SaveImageAsync(imageFile);
        if (newUpload != null)
        {
            card.UploadedImagePath = newUpload;
        }

        card.IsStarred = isStarred;

        _context.Flashcards.Update(card);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (!string.Equals(newUpload, oldUploadedImagePath, StringComparison.Ordinal))
            {
                DeleteUploadedImage(newUpload);
            }
            throw;
        }

        if (!string.Equals(oldUploadedImagePath, card.UploadedImagePath, StringComparison.Ordinal))
        {
            DeleteUploadedImage(oldUploadedImagePath);
        }
        return setId;
    }

    // Xóa thẻ khỏi bộ
    public async Task<int> DeleteCardAsync(int cardId, string userId)
    {
        Flashcard? card = await _context.Flashcards.FindAsync(cardId);
        if (card == null)
        {
            throw new KeyNotFoundException("Thẻ không tồn tại.");
        }

        int setId = card.FlashcardSetId;
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);

        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền xóa thẻ này.");
        }

        await _context.UserProgresses
            .Where(progress => progress.FlashcardId == cardId)
            .ExecuteDeleteAsync();

        await _context.DictationSessionDetails
            .Where(detail => detail.FlashcardId == cardId)
            .ExecuteDeleteAsync();

        await _context.EnglishMissionTargetWords
            .Where(word => word.FlashcardId == cardId)
            .ExecuteDeleteAsync();

        string? uploadedImagePath = card.UploadedImagePath;
        _context.Flashcards.Remove(card);
        await _context.SaveChangesAsync();
        DeleteUploadedImage(uploadedImagePath);
        return setId;
    }

    // Xóa toàn bộ thẻ trong một bộ thẻ (chủ sở hữu).
    // Load một lần rồi xóa tập trung, tránh vòng lặp xóa từng thẻ (N+1).
    public async Task DeleteAllCardsAsync(int setId, string userId)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);

        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền xóa thẻ trong bộ thẻ này.");
        }

        List<string> uploadedImagePaths = await RemoveAllCardsInternalAsync(setId);
        await _context.SaveChangesAsync();
        foreach (string path in uploadedImagePaths)
        {
            DeleteUploadedImage(path);
        }
    }

    // Xóa progress + thẻ của một bộ nhưng KHÔNG SaveChanges — dùng chung cho batch import trong transaction.
    private async Task<List<string>> RemoveAllCardsInternalAsync(int setId)
    {
        List<UserProgress> progresses = await _context.UserProgresses
            .Where(progress => progress.Flashcard!.FlashcardSetId == setId)
            .ToListAsync();
        _context.UserProgresses.RemoveRange(progresses);

        List<DictationSessionDetail> details = await _context.DictationSessionDetails
            .Where(detail => detail.Flashcard!.FlashcardSetId == setId)
            .ToListAsync();
        _context.DictationSessionDetails.RemoveRange(details);

        List<EnglishMissionTargetWord> missionWords = await _context.EnglishMissionTargetWords
            .Where(word => word.Flashcard!.FlashcardSetId == setId)
            .ToListAsync();
        _context.EnglishMissionTargetWords.RemoveRange(missionWords);

        List<Flashcard> cards = await _context.Flashcards
            .Where(card => card.FlashcardSetId == setId)
            .ToListAsync();
        _context.Flashcards.RemoveRange(cards);
        return cards
            .Select(card => card.UploadedImagePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
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
        if (cards.Count > FlashcardImportValidation.MaxRows)
        {
            throw new ArgumentException($"Mỗi lần chỉ được nhập tối đa {FlashcardImportValidation.MaxRows} thẻ.");
        }

        FlashcardSet? set = await _context.FlashcardSets
            .Include(row => row.Flashcards)
            .FirstOrDefaultAsync(row => row.Id == setId);

        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền thêm thẻ.");
        }

        int nextOrder = 0;
        List<string> uploadedImagePaths = [];
        if (replaceAll)
        {
            uploadedImagePaths = await RemoveAllCardsInternalAsync(setId);
        }
        else if (set.Flashcards.Any())
        {
            nextOrder = set.Flashcards.Max(card => card.OrderIndex) + 1;
        }

        List<Flashcard> created = new List<Flashcard>();
        foreach (BatchImportCardItem item in cards)
        {
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

            nextOrder++;
            created.Add(card);
        }

        await _context.Flashcards.AddRangeAsync(created);
        await _context.SaveChangesAsync();
        foreach (string path in uploadedImagePaths)
        {
            DeleteUploadedImage(path);
        }
        return created;
    }

    // Đổi trạng thái đánh sao của thẻ
    public async Task<bool> ToggleStarAsync(int cardId, string userId)
    {
        Flashcard? card = await _context.Flashcards.FindAsync(cardId);
        if (card == null)
        {
            throw new KeyNotFoundException("Thẻ không tồn tại.");
        }

        FlashcardSet? set = await _context.FlashcardSets.FindAsync(card.FlashcardSetId);

        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền chỉnh sửa thẻ này.");
        }

        card.IsStarred = !card.IsStarred;
        _context.Flashcards.Update(card);
        await _context.SaveChangesAsync();
        return card.IsStarred;
    }

    // Cập nhật thứ tự các thẻ theo mảng id được sắp xếp
    public async Task ReorderCardsAsync(int setId, int[] orderedCardIds, string userId)
    {
        FlashcardSet? set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null || set.UserId != userId)
        {
            throw new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.");
        }

        List<Flashcard> cards = await _context.Flashcards
            .Where(c => c.FlashcardSetId == setId)
            .ToListAsync();

        HashSet<int> cardIds = cards.Select(c => c.Id).ToHashSet();
        HashSet<int> orderedIds = orderedCardIds.ToHashSet();

        List<int> unknownIds = orderedIds.Except(cardIds).ToList();
        if (unknownIds.Count > 0)
        {
            throw new ArgumentException($"Các id thẻ không thuộc bộ thẻ: {string.Join(", ", unknownIds)}.");
        }

        List<int> missingIds = cardIds.Except(orderedIds).ToList();
        if (missingIds.Count > 0)
        {
            throw new ArgumentException($"Thiếu thứ tự cho các thẻ: {string.Join(", ", missingIds)}.");
        }

        if (orderedCardIds.Length != cardIds.Count)
        {
            throw new ArgumentException("Danh sách thứ tự chứa id thẻ trùng lặp.");
        }

        Dictionary<int, Flashcard> cardMap = cards.ToDictionary(c => c.Id);
        for (int i = 0; i < orderedCardIds.Length; i++)
        {
            cardMap[orderedCardIds[i]].OrderIndex = i;
        }

        await _context.SaveChangesAsync();
    }
}
