using Microsoft.EntityFrameworkCore;
using ltwnc.Data;
using ltwnc.Models.Entities;

namespace ltwnc.Repositories;

public class FlashcardSetRepository : IFlashcardSetRepository
{
    private readonly AppDbContext _context;

    public FlashcardSetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<FlashcardSet>> GetByUserIdAsync(string userId)
    {
        return await _context.FlashcardSets
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<FlashcardSet>> GetPublicSetsAsync()
    {
        return await _context.FlashcardSets
            .Where(s => s.IsPublic)
            .OrderByDescending(s => s.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    public async Task<List<FlashcardSet>> SearchPublicSetsAsync(string query)
    {
        return await _context.FlashcardSets
            .Where(s => s.IsPublic && s.Title.Contains(query))
            .OrderByDescending(s => s.UpdatedAt)
            .Take(20)
            .ToListAsync();
    }

    public async Task<FlashcardSet?> GetByIdAsync(int id)
    {
        return await _context.FlashcardSets.FindAsync(id);
    }

    public async Task<FlashcardSet?> GetByIdWithCardsAsync(int id)
    {
        return await _context.FlashcardSets
            .Include(s => s.Flashcards.OrderBy(f => f.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task AddAsync(FlashcardSet set)
    {
        await _context.FlashcardSets.AddAsync(set);
    }

    public void Update(FlashcardSet set)
    {
        _context.FlashcardSets.Update(set);
    }

    public void Delete(FlashcardSet set)
    {
        _context.FlashcardSets.Remove(set);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
