using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Services;

// Service xử lý nghiệp vụ học tập
// Quản lý tiến trình học (đã biết/chưa biết) và phiên học
public class StudyService
{
    private readonly AppDbContext _context;

    // Inject AppDbContext
    public StudyService(AppDbContext context)
    {
        _context = context;
    }

    // Lấy danh sách thẻ trong một bộ để học
    public async Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId, bool starredOnly = false, bool unlearnedOnly = false, string? userId = null)
    {
        var query = _context.Flashcards.Where(f => f.FlashcardSetId == setId);

        if (starredOnly)
        {
            query = query.Where(f => f.IsStarred);
        }

        if (unlearnedOnly && !string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(f => !_context.UserProgresses.Any(p => p.UserId == userId && p.FlashcardId == f.Id && p.IsLearned));
        }

        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
    }

    public async Task<UserStudySettings> GetSettingsAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return new UserStudySettings();

        var settings = await _context.UserStudySettings.FirstOrDefaultAsync(s => s.UserId == userId);
        return settings ?? new UserStudySettings { UserId = userId };
    }

    public async Task<UserStudySettings> SaveSettingsAsync(string userId, UserStudySettings input)
    {
        var settings = await _context.UserStudySettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null)
        {
            settings = new UserStudySettings { UserId = userId };
            await _context.UserStudySettings.AddAsync(settings);
        }

        settings.StarredOnly = input.StarredOnly;
        settings.UnlearnedOnly = input.UnlearnedOnly;
        settings.ShowFrontTerm = input.ShowFrontTerm;
        settings.ShowFrontDefinition = input.ShowFrontDefinition;
        settings.ShowFrontIpa = input.ShowFrontIpa;
        settings.ShowFrontImage = input.ShowFrontImage;
        settings.ShowBackTerm = input.ShowBackTerm;
        settings.ShowBackDefinition = input.ShowBackDefinition;
        settings.ShowBackIpa = input.ShowBackIpa;
        settings.ShowBackExample = input.ShowBackExample;
        settings.ShowBackImage = input.ShowBackImage;
        settings.HideImage = input.HideImage;
        settings.BlurImage = input.BlurImage;
        settings.LargeImage = input.LargeImage;
        settings.PronounceFront = input.PronounceFront;
        settings.PronounceBack = input.PronounceBack;

        await _context.SaveChangesAsync();
        return settings;
    }

    // Đánh dấu thẻ đã biết hoặc chưa biết
    public async Task MarkLearnedAsync(string userId, int setId, int flashcardId, bool learned)
    {
        var set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null)
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        if (!set.IsPublic && set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");

        var card = await _context.Flashcards.FindAsync(flashcardId);
        if (card == null || card.FlashcardSetId != setId)
            throw new KeyNotFoundException("Thẻ không tồn tại trong bộ thẻ này.");

        var progress = await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.FlashcardId == flashcardId);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                FlashcardId = flashcardId,
                IsLearned = learned,
                LastReviewed = DateTime.UtcNow
            };
            await _context.UserProgresses.AddAsync(progress);
        }
        else
        {
            progress.IsLearned = learned;
            progress.LastReviewed = DateTime.UtcNow;
            _context.UserProgresses.Update(progress);
        }

        await _context.SaveChangesAsync();
    }

    // Ghi nhận hoàn thành một phiên học
    public async Task CompleteSessionAsync(string userId, int setId, StudyMode mode)
    {
        var set = await _context.FlashcardSets.FindAsync(setId);
        if (set == null)
            throw new KeyNotFoundException("Bộ thẻ không tồn tại.");
        if (!set.IsPublic && set.UserId != userId)
            throw new UnauthorizedAccessException("Không có quyền học bộ thẻ này.");

        var session = new StudySession
        {
            UserId = userId,
            FlashcardSetId = setId,
            Mode = mode,
            CompletedAt = DateTime.UtcNow
        };
        await _context.StudySessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }
}
