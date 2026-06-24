using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public class StudySessionRepository : IStudySessionRepository
{
    private readonly AppDbContext _context;

    public StudySessionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StudySession session)
    {
        await _context.StudySessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }

    public async Task<UserProgress?> GetProgressAsync(string userId, int flashcardId)
    {
        return await _context.UserProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.FlashcardId == flashcardId);
    }

    public async Task<List<UserProgress>> GetProgressBySetAsync(string userId, int setId)
    {
        return await _context.UserProgresses
            .Where(p => p.UserId == userId && p.Flashcard!.FlashcardSetId == setId)
            .ToListAsync();
    }

    public void UpdateProgress(UserProgress progress)
    {
        _context.UserProgresses.Update(progress);
    }

    public async Task AddProgressAsync(UserProgress progress)
    {
        await _context.UserProgresses.AddAsync(progress);
    }
}
