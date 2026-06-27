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
    public async Task<List<Flashcard>> GetFlashcardsForStudyAsync(int setId, bool starredOnly = false)
    {
        var query = _context.Flashcards.Where(f => f.FlashcardSetId == setId);
        if (starredOnly)
        {
            query = query.Where(f => f.IsStarred);
        }
        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
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
