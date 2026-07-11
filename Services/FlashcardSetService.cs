using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;
using ltwnc.Models.ViewModels.FlashcardSet;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;

namespace ltwnc.Services;

// Service xử lý nghiệp vụ bộ thẻ flashcard
// Phân quyền: chỉ chủ sở hữu mới được sửa/xóa bộ thẻ và thẻ
public class FlashcardSetService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    // Inject AppDbContext
    public FlashcardSetService(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    private static string RequiredText(string? value, string fieldName)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException($"{fieldName} không được để trống.");
        }
        return trimmed;
    }

    private static string RequiredText(string? value, string fieldName, int maxLength)
    {
        var trimmed = RequiredText(value, fieldName);
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} tối đa {maxLength} ký tự.");
        }
        return trimmed;
    }

    private static string? OptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private async Task<string?> SaveImageAsync(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length == 0) return null;
        if (imageFile.Length > 2 * 1024 * 1024)
            throw new ArgumentException("Ảnh tối đa 2 MB.");

        var extension = Path.GetExtension(imageFile.FileName);
        if (!AllowedImageExtensions.Contains(extension))
            throw new ArgumentException("Ảnh chỉ hỗ trợ JPG, PNG hoặc WebP.");

        if (!AllowedImageContentTypes.Contains(imageFile.ContentType))
            throw new ArgumentException("Ảnh chỉ hỗ trợ JPG, PNG hoặc WebP.");

        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "flashcards");
        Directory.CreateDirectory(uploadRoot);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var absolutePath = Path.Combine(uploadRoot, fileName);
        await using var stream = File.Create(absolutePath);
        await imageFile.CopyToAsync(stream);

        return $"/uploads/flashcards/{fileName}";
    }

    // Lấy tất cả bộ thẻ thuộc về một người dùng
    public async Task<List<FlashcardSet>> GetMySetsAsync(string userId)
    {
        return await _context.FlashcardSets
            // ponytail: load cards for the existing view model; project counts if set sizes grow large.
            .Include(s => s.Flashcards)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
    }

    // Lấy tất cả bộ thẻ thuộc về một ngườidùng kèm tiến trình học
    public async Task<List<FlashcardSetListItemViewModel>> GetMySetsWithProgressAsync(string userId)
    {
        var sets = await _context.FlashcardSets
            .Include(s => s.Flashcards)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        var flashcardIds = sets.SelectMany(s => s.Flashcards).Select(f => f.Id).ToList();

        var learnedCardIds = await _context.UserProgresses
            .Where(p => p.UserId == userId && flashcardIds.Contains(p.FlashcardId) && p.IsLearned)
            .Select(p => p.FlashcardId)
            .ToListAsync();

        var learnedSet = new HashSet<int>(learnedCardIds);

        return sets.Select(s =>
        {
            var total = s.Flashcards.Count;
            var learned = s.Flashcards.Count(f => learnedSet.Contains(f.Id));
            return new FlashcardSetListItemViewModel
            {
                Set = s,
                TotalCards = total,
                LearnedCount = learned,
                MasteryPercent = total > 0 ? learned * 100 / total : 0
            };
        }).ToList();
    }

    // Lấy danh sách bộ thẻ public (mới nhất)
    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        return await _context.FlashcardSets
            .Where(s => s.IsPublic)
            .OrderByDescending(s => s.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    // Tìm kiếm bộ thẻ public theo tiêu đề
    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        return await _context.FlashcardSets
            .Where(s => s.IsPublic && s.Title.Contains(query))
            .OrderByDescending(s => s.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    // Lấy bộ thẻ theo id (không kèm thẻ)
    public async Task<FlashcardSet?> GetSetByIdAsync(int id)
    {
        return await _context.FlashcardSets.FindAsync(id);
    }

    public async Task<FlashcardSet?> GetAccessibleSetAsync(int id, string? userId)
    {
        var set = await _context.FlashcardSets.FindAsync(id);
        if (set == null || (!set.IsPublic && set.UserId != userId)) return null;
        return set;
    }

    // Lấy bộ thẻ kèm danh sách thẻ — chỉ trả về nếu người yêu cầu là chủ sở hữu
    public async Task<FlashcardSet?> GetSetWithCardsAsync(int id, string userId)
    {
        var set = await _context.FlashcardSets
            .Include(s => s.Flashcards.OrderBy(f => f.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == id);
        if (set == null || set.UserId != userId) return null;
        return set;
    }

    public async Task<FlashcardSet?> GetAccessibleSetWithCardsAsync(int id, string? userId)
    {
        var set = await _context.FlashcardSets
            .Include(s => s.Flashcards.OrderBy(f => f.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == id);
        if (set == null || (!set.IsPublic && set.UserId != userId)) return null;
        return set;
    }

    public async Task<FlashcardSet?> GetOwnedSetAsync(int id, string userId)
    {
        var set = await _context.FlashcardSets.FindAsync(id);
        if (set == null || set.UserId != userId) return null;
        return set;
    }

    public async Task<FlashcardSet?> GetExistingCopyAsync(int sourceSetId, string learnerId)
    {
        return await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == learnerId && s.SourceSetId == sourceSetId);
    }

    // Sao chép một bộ thẻ công khai vào thư viện riêng của ngườidùng
    public async Task<FlashcardSet> CopyPublicSetAsync(int sourceSetId, string learnerId)
    {
        var source = await _context.FlashcardSets
            .Include(s => s.Flashcards.OrderBy(f => f.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == sourceSetId);

        if (source == null || !source.IsPublic)
            throw new KeyNotFoundException("Bộ thẻ nguồn không tồn tại.");

        if (source.UserId == learnerId)
            throw new UnauthorizedAccessException("Không thể sao chép bộ thẻ của chính mình.");

        var existingCopy = await _context.FlashcardSets
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == learnerId && s.SourceSetId == sourceSetId);
        if (existingCopy != null)
            return existingCopy;

        var copy = source.Clone();
        copy.UserId = learnerId;
        copy.SourceSetId = source.Id;
        copy.IsPublic = false;

        await using var transaction = await _context.Database.BeginTransactionAsync();
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
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;

            var recovered = await _context.FlashcardSets
                .FirstOrDefaultAsync(s => s.UserId == learnerId && s.SourceSetId == sourceSetId);
            if (recovered != null)
                return recovered;

            throw;
        }
    }

    // Tạo bộ thẻ mới
    public async Task<FlashcardSet> CreateSetAsync(string title, string? description, bool isPublic, string userId)
    {
        var set = new FlashcardSet
        {
            Title = title,
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
    public async Task UpdateSetAsync(int id, string title, string? description, bool isPublic, string userId)
    {
        var set = await _context.FlashcardSets.FindAsync(id);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền sửa bộ thẻ này.");
        set.Title = title;
        set.Description = description;
        set.IsPublic = isPublic;
        set.UpdatedAt = DateTime.UtcNow;
        _context.FlashcardSets.Update(set);
        await _context.SaveChangesAsync();
    }

    // Xóa bộ thẻ
    public async Task DeleteSetAsync(int id, string userId)
    {
        var set = await _context.FlashcardSets.FindAsync(id);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xóa bộ thẻ này.");

        await _context.UserProgresses
            .Where(p => p.Flashcard!.FlashcardSetId == id)
            .ExecuteDeleteAsync();

        await _context.StudySessions
            .Where(s => s.FlashcardSetId == id)
            .ExecuteDeleteAsync();

        _context.FlashcardSets.Remove(set);
        await _context.SaveChangesAsync();
    }

    // Thêm thẻ mới vào bộ
    public async Task<Flashcard> AddCardAsync(
        int setId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool isStarred,
        string userId)
    {
        var set = await _context.FlashcardSets
            .Include(s => s.Flashcards)
            .FirstOrDefaultAsync(s => s.Id == setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền thêm thẻ.");

        frontText = RequiredText(frontText, "Thuật ngữ");
        backText = RequiredText(backText, "Định nghĩa");
        pronunciation = RequiredText(pronunciation, "IPA");
        partOfSpeech = RequiredText(partOfSpeech, "Loại từ", 80);
        exampleSentence = RequiredText(exampleSentence, "Ví dụ tiếng Anh");
        exampleMeaning = RequiredText(exampleMeaning, "Nghĩa câu ví dụ tiếng Việt");
        synonyms = OptionalText(synonyms);
        imageUrl = OptionalText(imageUrl);
        var uploadedImagePath = await SaveImageAsync(imageFile);

        var maxOrder = set.Flashcards.Any() ? set.Flashcards.Max(f => f.OrderIndex) : 0;
        var card = new Flashcard
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
            OrderIndex = maxOrder + 1
        };

        await _context.Flashcards.AddAsync(card);
        await _context.SaveChangesAsync();
        return card;
    }

    // Cập nhật nội dung thẻ (mặt trước + mặt sau)
    public async Task<int> UpdateCardAsync(
        int cardId,
        string frontText,
        string backText,
        string pronunciation,
        string partOfSpeech,
        string exampleSentence,
        string exampleMeaning,
        string? synonyms,
        string? imageUrl,
        IFormFile? imageFile,
        bool removeUploadedImage,
        bool isStarred,
        string userId)
    {
        var card = await _context.Flashcards.FindAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");

        var setId = card.FlashcardSetId;
        var set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền sửa thẻ này.");

        card.FrontText = RequiredText(frontText, "Thuật ngữ");
        card.BackText = RequiredText(backText, "Định nghĩa");
        card.Pronunciation = RequiredText(pronunciation, "IPA");
        card.PartOfSpeech = RequiredText(partOfSpeech, "Loại từ", 80);
        card.ExampleSentence = RequiredText(exampleSentence, "Ví dụ tiếng Anh");
        card.ExampleMeaning = RequiredText(exampleMeaning, "Nghĩa câu ví dụ tiếng Việt");
        card.Synonyms = OptionalText(synonyms);
        card.ImageUrl = OptionalText(imageUrl);

        if (removeUploadedImage)
        {
            card.UploadedImagePath = null;
        }

        var newUpload = await SaveImageAsync(imageFile);
        if (newUpload != null)
        {
            card.UploadedImagePath = newUpload;
        }

        card.IsStarred = isStarred;

        _context.Flashcards.Update(card);
        await _context.SaveChangesAsync();
        return setId;
    }

    // Xóa thẻ khỏi bộ
    public async Task<int> DeleteCardAsync(int cardId, string userId)
    {
        var card = await _context.Flashcards.FindAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");
        var setId = card.FlashcardSetId;
        var set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền xóa thẻ này.");

        await _context.UserProgresses
            .Where(p => p.FlashcardId == cardId)
            .ExecuteDeleteAsync();

        _context.Flashcards.Remove(card);
        await _context.SaveChangesAsync();
        return setId;
    }

    // Đổi trạng thái đánh sao của thẻ
    public async Task<bool> ToggleStarAsync(int cardId, string userId)
    {
        var card = await _context.Flashcards.FindAsync(cardId);
        if (card == null) throw new KeyNotFoundException("Thẻ không tồn tại.");

        var set = await _context.FlashcardSets.FindAsync(card.FlashcardSetId);
        if (set == null || set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền chỉnh sửa thẻ này.");

        card.IsStarred = !card.IsStarred;
        _context.Flashcards.Update(card);
        await _context.SaveChangesAsync();
        return card.IsStarred;
    }
}
